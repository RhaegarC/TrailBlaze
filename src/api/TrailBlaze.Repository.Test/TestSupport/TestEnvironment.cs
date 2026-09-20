namespace TrailBlaze.Repository.Test.TestSupport;

using Microsoft.Data.SqlClient;

/// <summary>
/// The one place the test tier resolves a connection string.
/// </summary>
/// <remarks>
/// <para>
/// The variables are named <c>TRAILBLAZE_*</c> rather than the application's own
/// <c>DbConnection</c> and <c>BlobConnection</c>, and that is the load-bearing decision here.
/// A developer with a working API setup has those in their shell already, pointing at a real
/// Azure SQL Database and a real storage account. A test tier that read them would have
/// <c>dotnet test</c> create and drop databases in production — so it reads names the
/// application never uses, and can only be aimed at something else deliberately.
/// </para>
/// <para>
/// The two defaults are deliberately asymmetric. Storage falls back to the Azurite emulator
/// because that needs no secret: the account key in
/// <see cref="AzuriteConnection"/> is Microsoft's published emulator key, a documented
/// constant rather than a credential, and it reaches nothing this machine did not start. The
/// database has no such fallback, because its password is exactly what the compose file
/// refuses to commit — so with nothing configured the tier skips and says why.
/// </para>
/// </remarks>
public static class TestEnvironment
{
    /// <summary>A whole SQL connection string. Overrides everything below.</summary>
    public const string SqlConnectionVariable = "TRAILBLAZE_SQL_CONNECTION";

    /// <summary>A whole storage connection string. Overrides the emulator default.</summary>
    public const string StorageConnectionVariable = "TRAILBLAZE_STORAGE_CONNECTION";

    /// <summary>The container's <c>sa</c> password. The compose file reads the same variable
    /// out of <c>src/api/.env</c>, which is why one exported value serves both.</summary>
    public const string PasswordVariable = "MSSQL_SA_PASSWORD";

    /// <summary>Where the compose file publishes the engine.</summary>
    private const string ContainerServer = "127.0.0.1,1433";

    /// <summary>Microsoft's published development key for the Azurite emulator. Public by
    /// design and not a credential — it is a fixed constant of a local emulator, and the
    /// account it opens is the one this machine started. It is here rather than in a
    /// configuration file because storage then needs no setup at all to run tests.</summary>
    private const string AzuriteKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/"
        + "K1SZFPTOtr/KBHBeksoGMGw==";

    /// <summary>The emulator's blob endpoint, which is the only service this solution uses.</summary>
    public static string AzuriteConnection =>
        "DefaultEndpointsProtocol=http;"
        + "AccountName=devstoreaccount1;"
        + $"AccountKey={AzuriteKey};"
        + "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    /// <summary>
    /// The database to test against, or the reason there is none — never both, and never
    /// neither.
    /// </summary>
    /// <returns>
    /// A connection string with an empty reason, or a null connection string with the reason
    /// a container-backed test should skip.
    /// </returns>
    /// <exception cref="InvalidOperationException">The configured string cannot work against
    /// the server it names — see <see cref="Validate"/>.</exception>
    public static (string? ConnectionString, string UnavailableReason) Database()
    {
        if (Read(SqlConnectionVariable) is { } configured)
        {
            Validate(configured);
            return (configured, string.Empty);
        }

        if (Read(PasswordVariable) is { } password)
        {
            return (Compose(password), string.Empty);
        }

        return (null, $"Not configured. Set {PasswordVariable} to the password the container was "
            + $"started with, or {SqlConnectionVariable} to a whole connection string. Both are "
            + "in src/api/.env for `docker compose -f docker-compose.test.yml up -d`.");
    }

    /// <summary>
    /// The storage account to test against. Never absent: the emulator needs no credentials,
    /// so this tier has no "not configured" state, only "not answering".
    /// </summary>
    public static string Storage() => Read(StorageConnectionVariable) ?? AzuriteConnection;

    /// <summary>
    /// Whether the storage account was named explicitly rather than falling back to the
    /// emulator.
    /// </summary>
    /// <remarks>
    /// Only used to word a skip: "nothing answered at the emulator, start it with ..." and
    /// "the account you configured is not answering" are the same failure with two different
    /// fixes, and the message cannot tell them apart without asking this.
    /// </remarks>
    public static bool StorageIsConfigured => Read(StorageConnectionVariable) is not null;

    /// <summary>The connection string for the container the compose file starts. Stated
    /// explicitly rather than left to defaults, because both of these are the subject of a
    /// documented contradiction — see <see cref="Validate"/>.</summary>
    public static string Compose(string password)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = ContainerServer,
            // Any catalog will do: the fixture issues its own CREATE/DROP DATABASE, which are
            // server-scoped. master is chosen because it always exists.
            InitialCatalog = "master",
            UserID = "sa",
            Password = password,
            TrustServerCertificate = true,
        };

        // Set through the indexer because `Encrypt` changed type in SqlClient 5.0 and the
        // string form is stable across that. It is the default in 6.x; writing it down is
        // what makes the rule in Validate a statement about this string rather than about
        // whatever the client library happens to default to.
        builder["Encrypt"] = "True";

        return builder.ConnectionString;
    }

    private static string? Read(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

    /// <summary>
    /// Rejects a string that cannot reach the server it names, so the failure is a sentence
    /// rather than an opaque TLS error.
    /// </summary>
    /// <remarks>
    /// Microsoft.Data.SqlClient 6.x encrypts by default, and Azure SQL Edge serves a
    /// self-signed certificate, so a loopback connection without
    /// <c>TrustServerCertificate=True</c> fails on the certificate. The error names neither
    /// the certificate nor the missing keyword, which is a debugging session for a one-word
    /// fix — so it is caught here instead. STANDARD §6 forbids the keyword for the
    /// <em>application</em>; this tier is the documented exception, and this method is where
    /// that exception is enforced rather than merely written down.
    /// </remarks>
    private static void Validate(string connectionString)
    {
        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"{SqlConnectionVariable} is not a connection string SqlClient can read: "
                + exception.Message,
                exception);
        }

        // Only a local server is checked: a remote one with a real certificate is a
        // legitimate configuration, and this tier is not going to tell an operator their
        // production connection string is wrong.
        if (!IsLoopback(builder.DataSource)
            || builder.TrustServerCertificate
            || EncryptionIsOff(builder))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{SqlConnectionVariable} points at {builder.DataSource} without "
            + "TrustServerCertificate=True. Azure SQL Edge serves a self-signed certificate and "
            + "SqlClient encrypts by default, so this fails with a certificate error that names "
            + "neither. Add TrustServerCertificate=True, or set Encrypt=Optional to make the "
            + "omission deliberate.");
    }

    private static bool IsLoopback(string dataSource)
    {
        string host = dataSource.Trim();

        // "tcp:127.0.0.1,1433", "127.0.0.1,1433", "localhost:1433" and "::1" all name the
        // same machine, and the port is never part of the answer.
        if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        int comma = host.IndexOf(',');
        if (comma >= 0)
        {
            host = host[..comma];
        }
        else if (!host.Contains("::"))
        {
            // Only strip a colon when it cannot be part of an IPv6 address — "::1" has no
            // port to remove, and splitting it there would leave ":1".
            int colon = host.IndexOf(':');
            if (colon >= 0)
            {
                host = host[..colon];
            }
        }

        return host.Trim() is "127.0.0.1" or "localhost" or "." or "(local)" or "::1";
    }

    /// <summary>An explicit opt-out, so the check above cannot reject a working string. Read
    /// through the indexer because <c>Encrypt</c>'s typed property changed shape in
    /// SqlClient 5.0 and this must survive that.</summary>
    private static bool EncryptionIsOff(SqlConnectionStringBuilder builder) =>
        builder.TryGetValue("Encrypt", out object? raw)
        && raw is string text
        && (text.Equals("Optional", StringComparison.OrdinalIgnoreCase)
            || text.Equals("False", StringComparison.OrdinalIgnoreCase)
            || text.Equals("No", StringComparison.OrdinalIgnoreCase));
}
