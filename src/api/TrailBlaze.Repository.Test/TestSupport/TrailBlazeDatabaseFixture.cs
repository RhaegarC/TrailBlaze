namespace TrailBlaze.Repository.Test.TestSupport;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The run's database, migrated to head: what every container test but the migration ones wants.
/// </summary>
/// <remarks>
/// <b>One database for the whole run.</b> Every class works against the same <c>TrailBlazeTest</c>
/// through a scope of its own, so a test is isolated by the rows it writes rather than by the
/// database it writes them to: an assertion here must be scoped to its own id, and nothing may
/// assert on a table as a whole. A transaction rolled back per test cannot replace that — a
/// read-back through the same context hits EF's change tracker, and a genuine second context needs
/// MSDTC, which SQL Edge has not. Reaching nothing at the configured endpoint skips; everything
/// past that check fails, because a migration that will not apply against a real engine is the
/// exact bug this tier exists to find.
/// </remarks>
public sealed class TrailBlazeDatabaseFixture : IAsyncLifetime
{
    private TestDatabase? _database;
    private ServiceProvider? _provider;

    /// <summary>False when there is no database to test against, in which case every test
    /// must skip rather than fail. Read it through <c>CreateHarness</c> or an equivalent that
    /// calls <c>Skip.IfNot</c>, rather than branching on it.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Why <see cref="IsAvailable"/> is false. Empty when it is true.</summary>
    public string SkipReason { get; private set; } = string.Empty;

    /// <summary>The connection string for the run's database.</summary>
    public string DatabaseConnectionString =>
        (_database ?? throw NotInitialized()).ConnectionString;

    public async Task InitializeAsync()
    {
        (string? connection, string reason) = TestEnvironment.Database();

        if (connection is null)
        {
            Unavailable(reason);
            return;
        }

        if (!await TestDatabase.AnswersAsync(connection))
        {
            Unavailable(
                "A connection string is set, but nothing answered at "
                + $"{new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).DataSource}. "
                + "Start it with `docker compose -f docker-compose.test.yml up -d` from src/api.");
            return;
        }

        _database = await TestDatabase.HeadAsync(connection);

        // Built after the database exists and before any test uses it, so every scope resolves
        // through the same registration the application uses.
        _provider = TestPersistence.Build(
            _database.ConnectionString, FakeUserContext.NoRequest());

        IsAvailable = true;
    }

    /// <summary>
    /// A scope over the run's database, through the application's own registration.
    /// </summary>
    /// <remarks>
    /// A scope rather than a context, so the caller cannot hold one open across tests: every
    /// call gets a fresh change tracker, which is what makes a read-back a database read
    /// instead of a view of what the last save left in memory. The scope must be disposed by
    /// the caller — <c>using IServiceScope scope = fixture.CreateScope()</c>.
    /// </remarks>
    public IServiceScope CreateScope() =>
        (_provider ?? throw NotInitialized()).CreateScope();

    /// <summary>
    /// The harness the audit tier saves through, or a skip naming why it cannot.
    /// </summary>
    /// <remarks>
    /// It skips rather than returning null, so a test cannot forget to check availability and
    /// fail with a null reference that says nothing about the missing container. The skip
    /// surfaces as a skipped test carrying <see cref="SkipReason"/>. <b>The calling test must be
    /// <c>[SkippableFact]</c>, not <c>[Fact]</c>:</b> only SkippableFact's runner turns the thrown
    /// <c>SkipException</c> into a skip, and under a plain <c>[Fact]</c> the test <em>fails</em>.
    /// </remarks>
    public AuditHarness CreateHarness(FakeUserContext? user = null)
    {
        Skip.IfNot(IsAvailable, SkipReason);
        return new AuditHarness(DatabaseConnectionString, user);
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
            _provider = null;
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
            _database = null;
        }
    }

    private static InvalidOperationException NotInitialized() =>
        new("The fixture has not been initialized, or is unavailable. Call "
            + "`Skip.IfNot(fixture.IsAvailable, fixture.SkipReason)` before this.");

    private void Unavailable(string reason)
    {
        IsAvailable = false;
        SkipReason = reason;
    }
}
