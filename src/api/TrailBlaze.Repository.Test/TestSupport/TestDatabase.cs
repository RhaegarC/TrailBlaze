namespace TrailBlaze.Repository.Test.TestSupport;

using Microsoft.Data.SqlClient;

/// <summary>
/// One database on the test server: created, addressable by its own connection string, and
/// dropped when it is disposed.
/// </summary>
/// <remarks>
/// Separate from the fixture that hands it out because a test occasionally needs a database
/// that is <em>not</em> the class's shared one. <c>MigrationNarrowingTests</c> is the case:
/// it stops the schema at an earlier migration, and a database cannot be re-stopped there
/// once anything has taken it to head — so each of its tests needs its own, created and
/// dropped around that one test.
/// </remarks>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _serverConnectionString;
    private bool _created;

    /// <summary>This database's name, unique per creation.</summary>
    public string Name { get; }

    /// <summary>A connection string naming this database, derived from the server's own so
    /// credentials and encryption settings are never restated here.</summary>
    public string ConnectionString { get; }

    private TestDatabase(string serverConnectionString, string name)
    {
        _serverConnectionString = serverConnectionString;
        Name = name;
        ConnectionString = new SqlConnectionStringBuilder(serverConnectionString)
        {
            InitialCatalog = name,
        }.ConnectionString;
    }

    /// <summary>
    /// Creates an empty database on the server the given string reaches.
    /// </summary>
    /// <remarks>
    /// Named after a generated GUID, so two runs — or two tests in one run — cannot collide,
    /// and a database left behind is self-evidently one whose cleanup was interrupted rather
    /// than a fixture someone forgot about.
    /// </remarks>
    public static async Task<TestDatabase> CreateAsync(string serverConnectionString)
    {
        var database = new TestDatabase(
            serverConnectionString, $"TrailBlazeTest_{Guid.NewGuid():N}");

        // Bracket-quoted because an unquoted identifier is a syntax error the moment a name
        // needs one. The value is a prefix this type wrote plus a generated GUID, never
        // anything a caller supplied, so there is no name here to sanitise.
        await database.RunOnServerAsync($"CREATE DATABASE [{database.Name}]");
        database._created = true;

        return database;
    }

    /// <summary>
    /// Whether anything answers at the given string, which is how a missing container is told
    /// apart from a wrong password or a wrong port.
    /// </summary>
    public static async Task<bool> AnswersAsync(string connectionString)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_created)
        {
            return;
        }

        _created = false;

        // EF pools connections, and a pooled connection to the database being dropped blocks
        // it. Clearing is cheaper and more reliable than waiting for the pool to age out.
        SqlConnection.ClearAllPools();

        // SINGLE_USER ... ROLLBACK IMMEDIATE evicts anything still attached. Without it the
        // DROP fails intermittently with "database in use", which would read as flakiness
        // rather than as the cleanup it is.
        await RunOnServerAsync(
            $"ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
            + $"DROP DATABASE [{Name}];");
    }

    /// <summary>Runs server-scoped DDL against whatever catalog the server string named.
    /// <c>CREATE DATABASE</c> and <c>DROP DATABASE</c> do not care which one that is.</summary>
    private async Task RunOnServerAsync(string sql)
    {
        await using var connection = new SqlConnection(_serverConnectionString);
        await connection.OpenAsync();

        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
