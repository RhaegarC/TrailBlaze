namespace TrailBlaze.Repository.Test;

using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// What a run leaves behind: its one database, and nothing else.
/// </summary>
/// <remarks>
/// The tier keeps its database so a failing assertion can be diagnosed by reading the rows behind
/// it, and the promise has two halves — the run's database outlives the run, and a database a test
/// made for itself does not. A fixture that dropped on dispose would break the first while the
/// second went on passing, which is why both are asserted.
/// </remarks>
[Trait("Category", "Container")]
public sealed class TestDatabaseRetentionTests(TrailBlazeServerFixture server)
    : IClassFixture<TrailBlazeServerFixture>
{
    /// <summary>The run's database is still on the server after the run lets go of it.</summary>
    [SkippableFact]
    public async Task The_runs_database_outlives_the_run()
    {
        Skip.IfNot(server.IsAvailable, server.SkipReason);

        TestDatabase head = await TestDatabase.HeadAsync(server.ServerConnectionString);
        await head.DisposeAsync();

        Assert.True(
            await TestDatabase.ExistsAsync(server.ServerConnectionString, TestDatabase.HeadName),
            $"{TestDatabase.HeadName} should survive its disposal, so the rows behind a failed "
            + "assertion can be read after the run.");
    }

    /// <summary>A database created for one test is gone once that test is.</summary>
    [SkippableFact]
    public async Task A_tests_own_database_does_not_outlive_it()
    {
        Skip.IfNot(server.IsAvailable, server.SkipReason);

        string name;
        await using (TestDatabase scratch = await server.CreateScratchDatabaseAsync())
        {
            name = scratch.Name;
        }

        Assert.False(await TestDatabase.ExistsAsync(server.ServerConnectionString, name));
    }
}
