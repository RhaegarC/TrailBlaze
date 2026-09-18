namespace TrailBlaze.Repository.Test.TestSupport;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A database of this fixture's own: created, given a schema, and dropped again.
/// </summary>
/// <remarks>
/// <para>
/// <b>One database per collection, not one per run and not a transaction per test.</b> A
/// transaction rolled back after each test was the obvious alternative and does not work:
/// reading a row back through the <em>same</em> context hits EF's change tracker, not the
/// database, so the round trip proves nothing — and a genuine second context needs a second
/// connection in the same transaction, which needs MSDTC, which a Linux SQL Edge container
/// does not have. Cleanup by deleting rows is the other alternative and is worse: this repo
/// has no hard delete at all (<c>IDbRepository.DeleteAsync</c> is a soft delete), so it would
/// mean raw SQL ordered by foreign key, extended by every future feature, rotting silently.
/// </para>
/// <para>
/// The cost is one <c>CREATE DATABASE</c> plus the migration set per collection — a second or
/// two — and the payoff is that isolation is structural rather than maintained: a class cannot
/// be polluted by another, and xUnit's parallelism survives.
/// </para>
/// <para>
/// <b>Where the skip boundary is.</b> Not being able to reach the configured server skips —
/// the container is simply not running. Everything after that is a failure, deliberately: a
/// migration that will not apply against a real engine is the exact bug this tier exists to
/// find, and degrading it to a skip would hide the one thing it was built for.
/// </para>
/// </remarks>
public abstract class DatabaseFixtureBase : IAsyncLifetime
{
    private TestDatabase? _database;
    private ServiceProvider? _provider;

    /// <summary>False when there is no database to test against, in which case every test
    /// must skip rather than fail. Read it through <c>CreateHarness</c> or an equivalent that
    /// calls <c>Skip.IfNot</c>, rather than branching on it.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Why <see cref="IsAvailable"/> is false. Empty when it is true.</summary>
    public string SkipReason { get; private set; } = string.Empty;

    /// <summary>This fixture's own database.</summary>
    protected TestDatabase Database =>
        _database ?? throw new InvalidOperationException(
            "The fixture has not been initialized, or is unavailable. Call "
            + "`Skip.IfNot(fixture.IsAvailable, fixture.SkipReason)` before this.");

    /// <summary>The connection string for this fixture's own database.</summary>
    public string DatabaseConnectionString => Database.ConnectionString;

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
                $"A connection string is set, but nothing answered at "
                + $"{new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).DataSource}. "
                + "Start it with `docker compose -f docker-compose.test.yml up -d` from src/api.");
            return;
        }

        _database = await TestDatabase.CreateAsync(connection);

        // Built once, after the database exists and before the schema is applied, so the
        // schema path and every test scope resolve through the same registration the
        // application uses.
        _provider = TestPersistence.Build(
            Database.ConnectionString, FakeUserContext.NoRequest());

        await ApplySchemaAsync();
        IsAvailable = true;
    }

    /// <summary>
    /// A scope over this fixture's database, through the application's own registration.
    /// </summary>
    /// <remarks>
    /// A scope rather than a context, so the caller cannot hold one open across tests: every
    /// call gets a fresh change tracker, which is what makes a read-back a database read
    /// instead of a view of what the last save left in memory. The scope must be disposed by
    /// the caller — <c>using IServiceScope scope = fixture.CreateScope()</c>.
    /// </remarks>
    public IServiceScope CreateScope() =>
        (_provider ?? throw new InvalidOperationException(
            "The fixture has not been initialized, or is unavailable. Call "
            + "`Skip.IfNot(fixture.IsAvailable, fixture.SkipReason)` before this."))
        .CreateScope();

    /// <summary>
    /// Brings this fixture's empty database up to the schema the tests expect.
    /// </summary>
    /// <remarks>
    /// Abstract because not every fixture wants the same schema, or any: the migrated fixture
    /// applies the migration set, and a fixture that only proves the server answers does
    /// nothing here at all.
    /// </remarks>
    protected abstract Task ApplySchemaAsync();

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

    private void Unavailable(string reason)
    {
        IsAvailable = false;
        SkipReason = reason;
    }
}
