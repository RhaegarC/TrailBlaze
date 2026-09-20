namespace TrailBlaze.Repository.Test.TestSupport;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Repository;

/// <summary>
/// One database on the test server: created, addressable by its own connection string, and left
/// in place when it is disposed so its rows can be read after the run.
/// </summary>
/// <remarks>
/// <b>One database for the whole run.</b> <see cref="HeadAsync"/> hands out <c>TrailBlazeTest</c>,
/// the database every container test shares, migrated to head and left standing when the run ends
/// so a failing assertion is diagnosed by reading the rows behind it; the next run drops it before
/// creating its own, so one run's worth survives and the engine does not fill up with them.
/// <see cref="ScratchAsync"/> is the other half — a private database for the tests that must drive
/// a schema of their own, dropped with the test that made it, so the run leaves nothing behind but
/// the database it means to leave.
/// </remarks>
public sealed class TestDatabase : IAsyncDisposable
{
    /// <summary>The database a run works against.</summary>
    public const string HeadName = "TrailBlazeTest";

    /// <summary>What a test's own database is named under. Distinct from the head's name so the
    /// head's cleanup can never be the thing that drops one.</summary>
    private const string ScratchName = "TrailBlazeScratch";

    /// <summary>SQL Server's "Cannot drop database … because it is currently in use".</summary>
    private const int CannotDropInUse = 3702;

    private static readonly object HeadLock = new();
    private static Task<TestDatabase>? _head;

    private readonly string _serverConnectionString;
    private readonly bool _scratch;

    /// <summary>This database's name.</summary>
    public string Name { get; }

    /// <summary>A connection string naming this database, derived from the server's own so
    /// credentials and encryption settings are never restated here.</summary>
    public string ConnectionString { get; }

    private TestDatabase(string serverConnectionString, string name, bool scratch)
    {
        _serverConnectionString = serverConnectionString;
        _scratch = scratch;
        Name = name;

        var builder = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = name };
        ConnectionString = builder.ConnectionString;
    }

    /// <summary>
    /// The run's database, migrated to head. Every call in a process returns the same one.
    /// </summary>
    public static Task<TestDatabase> HeadAsync(string serverConnectionString)
    {
        // Once per process: created and migrated here rather than per fixture, so test classes
        // initializing in parallel cannot race each other over __EFMigrationsHistory.
        lock (HeadLock)
        {
            return _head ??= CreateHeadAsync(serverConnectionString);
        }
    }

    /// <summary>
    /// A new, empty database of this test's own, dropped when it is disposed.
    /// </summary>
    /// <remarks>
    /// It waits for <see cref="HeadAsync"/> first, which is what makes the head's cleanup of
    /// leftovers safe: no database this process creates can exist while that runs, so the only
    /// scratch database it can find belongs to a run that ended or was interrupted.
    /// </remarks>
    public static async Task<TestDatabase> ScratchAsync(string serverConnectionString)
    {
        await HeadAsync(serverConnectionString);

        var database = new TestDatabase(
            serverConnectionString, $"{ScratchName}_{Guid.NewGuid():N}", scratch: true);

        await database.RunOnServerAsync($"CREATE DATABASE [{database.Name}]");

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

    /// <summary>Whether a database of the given name exists on the server.</summary>
    public static async Task<bool> ExistsAsync(string serverConnectionString, string name)
    {
        await using var connection = new SqlConnection(serverConnectionString);
        await connection.OpenAsync();

        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
        command.Parameters.AddWithValue("@name", name);

        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    /// <summary>Drops this database. The run's own is kept.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!_scratch)
        {
            return;
        }

        // SINGLE_USER ... ROLLBACK IMMEDIATE evicts whatever the connection pool is still
        // holding; without it the DROP fails intermittently with "database in use", which would
        // read as flakiness rather than as the cleanup it is.
        await RunOnServerAsync(
            $"ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}];");
    }

    private static async Task<TestDatabase> CreateHeadAsync(string serverConnectionString)
    {
        var database = new TestDatabase(serverConnectionString, HeadName, scratch: false);

        try
        {
            await database.RunOnServerAsync($"""
                -- Plain, so the engine refuses it while another run is connected: that refusal is
                -- what stops two runs sharing one name.
                IF DB_ID(N'{HeadName}') IS NOT NULL
                    DROP DATABASE [{HeadName}];

                -- Then what an interrupted run left; each in its own TRY/CATCH, so one still in
                -- use is skipped rather than dropped or made fatal.
                DECLARE @leftovers nvarchar(max) = N'';
                SELECT @leftovers = @leftovers
                    + N'BEGIN TRY DROP DATABASE ' + QUOTENAME(name) + N' END TRY BEGIN CATCH END CATCH;'
                FROM sys.databases
                WHERE name LIKE N'{HeadName}[_]%' OR name LIKE N'{ScratchName}[_]%';
                EXEC sp_executesql @leftovers;

                CREATE DATABASE [{HeadName}];
                """);
        }
        catch (SqlException failure) when (failure.Number == CannotDropInUse)
        {
            throw new InvalidOperationException(
                $"{HeadName} is in use by another test run. The tier keeps one database and two "
                + "runs cannot share it; wait for the other run to finish.", failure);
        }

        // Migrate, never EnsureCreated: EnsureCreated builds the schema from the model and
        // bypasses Migrations/, so a broken migration stays broken and the suite still passes —
        // a test that cannot fail. It also closes the door behind it, leaving a database with no
        // __EFMigrationsHistory that a later Migrate would try to create tables into.
        await using ServiceProvider provider = TestPersistence.Build(
            database.ConnectionString, FakeUserContext.NoRequest());

        using IServiceScope scope = provider.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        await context.Database.MigrateAsync();

        return database;
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
