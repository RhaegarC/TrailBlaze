namespace TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// A reachable test server, and nothing else. For tests that must build their own schema and
/// so cannot share the run's database with anyone.
/// </summary>
/// <remarks>
/// <see cref="TrailBlazeDatabaseFixture"/> gives a class the run's migrated database. That is
/// wrong for a migration test twice over: stopping the schema at an earlier migration is a state
/// the next test must not inherit, and a database cannot be returned to it once anything has taken
/// it to head. So these tests get a <see cref="CreateScratchDatabaseAsync">scratch database</see>
/// of their own, dropped when they finish, and this fixture supplies only the two things they
/// cannot create for themselves — a server that answers, and the skip when it does not.
/// </remarks>
public sealed class TrailBlazeServerFixture : IAsyncLifetime
{
    private string _connectionString = string.Empty;

    /// <summary>False when there is no server to test against, in which case every test must
    /// skip rather than fail.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Why <see cref="IsAvailable"/> is false. Empty when it is true.</summary>
    public string SkipReason { get; private set; } = string.Empty;

    /// <summary>The server this fixture reached, for a test that must run its own DDL against it
    /// rather than through <see cref="CreateScratchDatabaseAsync"/>.</summary>
    public string ServerConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        (string? connection, string reason) = TestEnvironment.Database();

        if (connection is null)
        {
            SkipReason = reason;
            return;
        }

        if (!await TestDatabase.AnswersAsync(connection))
        {
            SkipReason =
                $"A connection string is set, but nothing answered at "
                + $"{new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).DataSource}. "
                + "Start it with `docker compose -f docker-compose.test.yml up -d` from src/api.";
            return;
        }

        _connectionString = connection;
        IsAvailable = true;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// A new, empty database on this server, which the caller disposes and the dispose drops.
    /// </summary>
    /// <remarks>
    /// Skips rather than throwing when the server is absent, so a test cannot forget to check
    /// availability and fail with a connection error that says nothing about the container.
    /// The calling test must be <c>[SkippableFact]</c> — under a plain <c>[Fact]</c> the
    /// <c>SkipException</c> is just an exception and the test fails.
    /// </remarks>
    public async Task<TestDatabase> CreateScratchDatabaseAsync()
    {
        Skip.IfNot(IsAvailable, SkipReason);

        return await TestDatabase.ScratchAsync(_connectionString);
    }
}
