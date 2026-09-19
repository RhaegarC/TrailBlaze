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
/// <see cref="UserModelTests"/> asserts what the model declares, which is a claim about metadata.
/// This is the harder one the deployment rests on: the migration must reach head against a table
/// written under the old schema. Each test takes its own database, because they drive one to
/// <c>InitialCreate</c> and write rows the model would not accept — a state the shared fixture,
/// migrated on construction, would already have taken past.
/// </remarks>
[Trait("Category", "Container")]
public sealed class UserRoleMigrationTests(TrailBlazeServerFixture server)
    : IClassFixture<TrailBlazeServerFixture>
{
    /// <summary>The migration that creates <c>Users</c>, with <c>Role</c> nullable.</summary>
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
    /// Three rows for three answers. <c>NULL</c> is the case the backfill exists for.
    /// <c>SuperUser</c> is the one it would be easy to forget — not null, so a
    /// <c>WHERE Role IS NULL</c> backfill leaves it for the check constraint to refuse, stopping
    /// the deploy. <c>Admin</c> is the control: a backfill written carelessly would demote the
    /// one row that grants something.
    /// </remarks>
    [SkippableFact]
    public async Task Rows_written_before_the_constraint_survive_it()
    {
        await using TestDatabase database = await server.CreateScratchDatabaseAsync();

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
    /// The error number is asserted, not just "it threw": a length failure is also an exception,
    /// and accepting any of them would read as though the set were enforced when what rejected
    /// the row was its width. <c>SuperUser</c> is nine characters against a sixteen-character
    /// column, so no width rule can be what refuses it.
    /// </remarks>
    [SkippableFact]
    public async Task The_engine_refuses_a_role_outside_the_closed_set()
    {
        await using TestDatabase database = await server.CreateScratchDatabaseAsync();

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
    /// was given one — the fact that holds for a writer which is not this application.
    /// </remarks>
    [SkippableFact]
    public async Task The_database_applies_the_user_default_itself()
    {
        await using TestDatabase database = await server.CreateScratchDatabaseAsync();

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
