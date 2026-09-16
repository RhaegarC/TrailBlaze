namespace TrailBlaze.Repository
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Design;
    using TrailBlaze.Model;

    /// <summary>
    /// Lets <c>dotnet ef</c> resolve the context without booting the API — and so without the
    /// API's other startup requirements.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without this, EF falls back to running the startup project's <c>Program</c>, which
    /// requires <c>DbConnection</c>, <c>BlobConnection</c> and <c>AllowedOrigins</c> before it
    /// will build a host — so generating a migration would depend on every unrelated secret.
    /// </para>
    /// <para>
    /// Migrations are applied by the deployment pipeline, not at API startup: Azure Container Apps
    /// runs several replicas, and replicas migrating concurrently on startup race each other over
    /// the same DDL. That makes this factory the thing that decides <em>which</em> database gets
    /// migrated, so it reads the connection string from <see cref="Constant.ConfigKey.DBCon"/> in
    /// the environment — the one source that carries a real value, since the pipeline passes it
    /// and <c>appsettings.json</c> declares the key empty by design. It refuses to guess: a blank
    /// value throws rather than falling back to a local default that would migrate the wrong
    /// database quietly.
    /// </para>
    /// <para>
    /// The application itself reads configuration, layering environment over JSON. A design-time
    /// factory has no host to do that from, and pulling the configuration packages into this
    /// layer to read a key whose committed value is empty would buy nothing. So it reads the
    /// environment, and <c>DbConnection="..." dotnet ef database update</c> is how a developer
    /// running it by hand supplies one.
    /// </para>
    /// <para>
    /// <c>dotnet ef migrations add</c> needs only a syntactically valid value, not a reachable
    /// one: it selects the provider and opens nothing.
    /// </para>
    /// </remarks>
    internal sealed class TrailBlazeContextFactory : IDesignTimeDbContextFactory<TrailBlazeContext>
    {
        public TrailBlazeContext CreateDbContext(string[] args)
        {
            string? connectionString = Environment.GetEnvironmentVariable(Constant.ConfigKey.DBCon);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"'{Constant.ConfigKey.DBCon}' is not set in the environment, so there is no "
                    + "database to act on. The deployment pipeline supplies it; to run this by "
                    + $"hand, prefix the command with {Constant.ConfigKey.DBCon}=\"...\". For "
                    + "'migrations add' any well-formed connection string will do, because no "
                    + "connection is opened.");
            }

            var options = new DbContextOptionsBuilder<TrailBlazeContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new TrailBlazeContext(options);
        }
    }
}
