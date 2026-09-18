namespace TrailBlaze.Repository.Test;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// What a narrowing <c>ALTER COLUMN</c> does to a table that already holds data.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddUserProfileColumns</c> is the only migration here that changes an existing column
/// rather than adding one, and its own remarks say a populated table carrying a
/// <c>DisplayName</c> longer than the new bound stops the deploy with "String or binary data
/// would be truncated". That is a claim about the engine, and nothing in the suite could reach
/// it: the model tests inspect metadata, so they answer what the schema <em>is</em> and never
/// what happens to a row that does not fit it.
/// </para>
/// <para>
/// <b>Two tests, and both are needed.</b> The failing one alone could be red for the wrong
/// reason — a bad raw <c>INSERT</c>, a migration that will not apply for an unrelated cause, an
/// exception of the right kind from the wrong place. The passing one runs the identical path
/// with a value that fits, so the only difference between red and green is the length of one
/// string. That is what makes the red test mean "the data did not fit" rather than "something
/// about this test is broken".
/// </para>
/// <para>
/// <b>A database each, not the class's shared one.</b> Taking the schema to
/// <c>InitialCreate</c> is a state no later test may inherit, and it is not reachable again
/// once anything has taken that database to head. The shared fixture migrates on construction
/// and would defeat both of these before they started.
/// </para>
/// </remarks>
[Trait("Category", "Container")]
public sealed class MigrationNarrowingTests(TrailBlazeServerFixture server)
    : IClassFixture<TrailBlazeServerFixture>
{
    /// <summary>The migration that creates <c>Users</c>, with <c>DisplayName</c> unbounded.
    /// </summary>
    private const string InitialCreate = "20260915144435_InitialCreate";

    /// <summary>
    /// SQL Server's two "String or binary data would be truncated" codes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two, because which one arrives depends on the database's compatibility level and nothing
    /// else. <b>8152</b> is the legacy generic message — "String or binary data would be
    /// truncated" — and is what a database below level 150 raises. <b>2628</b> is the detailed
    /// replacement added in SQL Server 2019, which names the table, the column and the truncated
    /// value; it is raised at level 150 and above, and a database created at level 160 never
    /// produces 8152 at all. Measured rather than assumed: this container's <c>model</c> is at
    /// 160, so every <c>TrailBlazeTest_*</c> database inherits it and returns 2628.
    /// </para>
    /// <para>
    /// Asserting either number alone would pin this test to one compatibility level. The claim
    /// worth making is the one that holds at both: <em>the deploy stops</em>, and it stops for
    /// truncation rather than succeeding by discarding the row it could not fit. A set does
    /// that, and still fails loudly on any other exception — which is what stops this test
    /// passing for the wrong reason.
    /// </para>
    /// </remarks>
    private static readonly int[] TruncationErrors = [8152, 2628];

    [SkippableFact]
    public async Task A_narrowing_alter_fails_when_a_stored_value_no_longer_fits()
    {
        await using TestDatabase database = await server.CreateDatabaseAsync();

        Exception? thrown = await MigrateOverAsync(
            database, new string('x', Constant.UserProfile.DisplayNameLength + 100));

        SqlException? failure = SqlFailures.Find(thrown);

        Assert.True(
            failure is not null,
            $"{InitialCreate} plus an over-long DisplayName should have stopped the next "
            + "migration with a truncation error. Got "
            + $"{thrown?.GetType().Name ?? "no exception"}: {thrown?.Message}");

        Assert.Contains(failure.Number, TruncationErrors);
    }

    /// <summary>
    /// The identical path with a value that fits, so the test above is red because of the
    /// length and not because of anything else in it.
    /// </summary>
    [SkippableFact]
    public async Task The_same_narrowing_alter_succeeds_when_the_data_fits()
    {
        await using TestDatabase database = await server.CreateDatabaseAsync();
        string displayName = new('x', Constant.UserProfile.DisplayNameLength);

        Exception? thrown = await MigrateOverAsync(database, displayName);

        Assert.True(
            thrown is null,
            $"{InitialCreate} plus a DisplayName of exactly {displayName.Length} characters "
            + $"should migrate cleanly to head. Got {thrown?.GetType().Name}: {thrown?.Message}");

        // Migrating "successfully" by discarding the value that did not fit would satisfy the
        // assertion above, and is the outcome the migration's own remarks are guarding against.
        await using ServiceProvider provider = TestPersistence.Build(
            database.ConnectionString, FakeUserContext.NoRequest());

        using IServiceScope scope = provider.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        User stored = await context.Users.AsNoTracking().SingleAsync();
        Assert.Equal(displayName, stored.DisplayName);
    }

    /// <summary>
    /// Runs the whole path — schema at <c>InitialCreate</c>, one row, then migrate to head —
    /// and hands back whatever the final migration threw, or <c>null</c> if it applied.
    /// </summary>
    private static async Task<Exception?> MigrateOverAsync(
        TestDatabase database, string displayName)
    {
        await using ServiceProvider provider = TestPersistence.Build(
            database.ConnectionString, FakeUserContext.NoRequest());

        using IServiceScope scope = provider.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        IMigrator migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(InitialCreate);

        // Raw rather than through the context: at this point the model no longer matches the
        // schema — DisplayName is bounded in the model and unbounded in the table — so an
        // insert EF composed would be shaped by the head schema rather than by what is actually
        // there. Both datetimeoffset columns are NOT NULL and have no default, so the raw form
        // has to supply them.
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO Users (Id, DisplayName, CreatedOn, LastModifiedOn) "
            + "VALUES ({0}, {1}, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())",
            Guid.NewGuid().ToString(),
            displayName);

        return await Record.ExceptionAsync(() => migrator.MigrateAsync());
    }
}
