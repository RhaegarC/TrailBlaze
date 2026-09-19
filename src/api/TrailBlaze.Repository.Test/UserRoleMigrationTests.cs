namespace TrailBlaze.Repository.Test;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// What the role constraint does to a <c>users</c> table that already holds rows.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="UserModelTests"/> asserts what the model declares — non-nullable, defaulted,
/// limited to <c>User</c> and <c>Admin</c> — and that is a claim about metadata. The claim the
/// deployment rests on is different and harder: the migration must reach head against a table
/// that was written under the old schema. Three things could go wrong there and none of them is
/// visible from the model. A bare <c>ALTER COLUMN … NOT NULL</c> fails outright when even one
/// row holds <c>NULL</c>, so the backfill has to run first. A check constraint added before the
/// backfill is rejected by the data it is meant to describe. And a constraint that exists but
/// admits everything refuses nothing, which is what the third test below is for.
/// </para>
/// <para>
/// <b>A database each, not the class's shared one.</b> These tests drive a database to
/// <c>InitialCreate</c> and write rows the model would not accept, which is a state no later
/// test may inherit and which the shared fixture — migrated on construction — would have
/// already taken past.
/// </para>
/// </remarks>
[Trait("Category", "Container")]
public sealed class UserRoleMigrationTests(TrailBlazeServerFixture server)
    : IClassFixture<TrailBlazeServerFixture>
{
    /// <summary>The migration that creates <c>Users</c>, with <c>Role</c> unbounded and
    /// nullable — the shape of every row written before feature 03.</summary>
    private const string InitialCreate = "20260915144435_InitialCreate";

    /// <summary>The constraint the model declares, as the database names it.</summary>
    private const string RoleConstraint = "CK_Users_Role";

    /// <summary>SQL Server's CHECK-constraint violation.</summary>
    private const int CheckConstraintViolation = 547;

    /// <summary>
    /// A table that predates the constraint comes through the migration with its rows intact
    /// and the column tightened.
    /// </summary>
    /// <remarks>
    /// Three rows, because there are three answers and only one of them is the obvious one.
    /// <c>NULL</c> is the case the backfill exists for. <c>SuperUser</c> is the case it would be
    /// easy to forget: it is not null, so a <c>WHERE Role IS NULL</c> backfill leaves it in
    /// place — and the check constraint then fails against a row the deploy never intended to
    /// keep, which stops the migration rather than the request that would have read it.
    /// <c>Admin</c> is the control: a legitimate value must survive untouched, or the backfill
    /// would silently demote the administrator — the one row whose <c>Role</c> is not the
    /// default, and the one a backfill written carelessly would not think to spare.
    /// </remarks>
    [SkippableFact]
    public async Task Rows_written_before_the_constraint_survive_it()
    {
        await using TestDatabase database = await server.CreateDatabaseAsync();

        await using ServiceProvider provider = TestPersistence.Build(
            database.ConnectionString, FakeUserContext.NoRequest());

        using IServiceScope scope = provider.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();
        IMigrator migrator = context.Database.GetService<IMigrator>();

        await migrator.MigrateAsync(InitialCreate);

        // Raw rather than through the context: at this point the model no longer matches the
        // schema — Role is required in the model and nullable in the table — so an insert EF
        // composed would be shaped by the head schema rather than by what is actually there.
        // Both datetimeoffset columns are NOT NULL and carry no default, so the raw form
        // supplies them.
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Users (Id, DisplayName, Role, CreatedOn, LastModifiedOn) VALUES
              ('pre-dates-the-column', 'no role at all', NULL,      SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
              ('never-was-a-role',     'some other value', 'SuperUser', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()),
              ('was-already-admin',    'the existing admin', 'Admin', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())
            """);

        await migrator.MigrateAsync();

        Assert.Equal("User", await RoleOfAsync(context, "pre-dates-the-column"));
        Assert.Equal("User", await RoleOfAsync(context, "never-was-a-role"));
        Assert.Equal("Admin", await RoleOfAsync(context, "was-already-admin"));

        // The value is only half of it: a column that still admits NULL would let the next
        // write reintroduce the state the backfill just cleared.
        Assert.Equal("NO", await IsNullableAsync(context));
    }

    /// <summary>
    /// The engine refuses a role outside the closed set, and says so as a constraint
    /// violation rather than as a length error.
    /// </summary>
    /// <remarks>
    /// The number is asserted, not just "it threw". A length failure is also an exception, and
    /// a bare <c>Assert.ThrowsAsync</c> would accept it — reading as though the closed set were
    /// enforced when what actually rejected the row was its width. <c>SuperUser</c> is nine
    /// characters against a sixteen-character column, so no width rule can be what refuses it.
    /// </remarks>
    [SkippableFact]
    public async Task The_engine_refuses_a_role_outside_the_closed_set()
    {
        await using TestDatabase database = await server.CreateDatabaseAsync();

        await using ServiceProvider provider = TestPersistence.Build(
            database.ConnectionString, FakeUserContext.NoRequest());

        using IServiceScope scope = provider.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        await context.Database.MigrateAsync();

        Exception? thrown = await Record.ExceptionAsync(() => context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Users (Id, DisplayName, Role, CreatedOn, LastModifiedOn)
            VALUES ('would-be-admin', 'not a role', 'SuperUser', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())
            """));

        SqlException? failure = SqlFailures.Find(thrown);

        Assert.True(
            failure is not null,
            "A role outside the closed set should have been refused by the check constraint. Got "
            + $"{thrown?.GetType().Name ?? "no exception"}: {thrown?.Message}");

        Assert.Equal(CheckConstraintViolation, failure.Number);
        Assert.Contains(RoleConstraint, failure.Message);
    }

    /// <summary>
    /// A row inserted without naming a role lands on <c>User</c>, by the database's own default
    /// rather than by anything this app does.
    /// </summary>
    /// <remarks>
    /// <see cref="UserModelTests"/> proves the model carries a default; this proves SQL Server
    /// was given one. The two are separate facts and the second is the one that holds for a
    /// writer which is not this application — a script, a support query, a future migration.
    /// <c>User</c> rather than <c>Admin</c> for the reason the whole column is arranged this
    /// way: the value applied when nobody said anything must be the one that grants nothing.
    /// </remarks>
    [SkippableFact]
    public async Task The_database_applies_the_user_default_itself()
    {
        await using TestDatabase database = await server.CreateDatabaseAsync();

        await using ServiceProvider provider = TestPersistence.Build(
            database.ConnectionString, FakeUserContext.NoRequest());

        using IServiceScope scope = provider.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        await context.Database.MigrateAsync();

        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Users (Id, DisplayName, CreatedOn, LastModifiedOn)
            VALUES ('no-role-named', 'inserted by something else', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())
            """);

        Assert.Equal("User", await RoleOfAsync(context, "no-role-named"));
    }

    private static Task<string> RoleOfAsync(TrailBlazeContext context, string id) =>
        context.Database
            .SqlQueryRaw<string>("SELECT Role AS [Value] FROM Users WHERE Id = {0}", id)
            .SingleAsync();

    private static Task<string> IsNullableAsync(TrailBlazeContext context) =>
        context.Database
            .SqlQueryRaw<string>(
                "SELECT IS_NULLABLE AS [Value] FROM INFORMATION_SCHEMA.COLUMNS "
                + "WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'Role'")
            .SingleAsync();
}
