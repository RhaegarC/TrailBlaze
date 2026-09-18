namespace TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// A reachable test server, and nothing else. For tests that must build their own schema and
/// so cannot share a database with anyone.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TrailBlazeDatabaseFixture"/> gives a class one migrated database. That is wrong
/// for a migration test twice over: stopping the schema at an earlier migration is a state the
/// next test must not inherit, and a database cannot be returned to it once anything has taken
/// it to head. So these tests get a database each, created and dropped around the single test
/// that needs it, and this fixture supplies only the two things they cannot create for
/// themselves — a server that answers, and the skip when it does not.
/// </para>
/// <para>
/// It deliberately creates no database of its own. Doing so would be a wasted
/// <c>CREATE</c>/<c>DROP</c> per class, and a member nothing reads is the scaffolding smell the
/// debt register already files.
/// </para>
/// </remarks>
public sealed class TrailBlazeServerFixture : IAsyncLifetime
{
    private string _connectionString = string.Empty;

    /// <summary>False when there is no server to test against, in which case every test must
    /// skip rather than fail.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Why <see cref="IsAvailable"/> is false. Empty when it is true.</summary>
    public string SkipReason { get; private set; } = string.Empty;

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
    /// A new, empty database on this server, which the caller disposes.
    /// </summary>
    /// <remarks>
    /// Skips rather than throwing when the server is absent, so a test cannot forget to check
    /// availability and fail with a connection error that says nothing about the container.
    /// The calling test must be <c>[SkippableFact]</c> — under a plain <c>[Fact]</c> the
    /// <c>SkipException</c> is just an exception and the test fails.
    /// </remarks>
    public async Task<TestDatabase> CreateDatabaseAsync()
    {
        Skip.IfNot(IsAvailable, SkipReason);

        return await TestDatabase.CreateAsync(_connectionString);
    }
}
