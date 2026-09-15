namespace TrailBlaze.Repository
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Design;

    /// <summary>
    /// Lets <c>dotnet ef</c> build the model at design time without booting the API.
    /// </summary>
    /// <remarks>
    /// Without this, EF falls back to running the startup project's <c>Program</c>, which now
    /// requires <c>DbConnection</c>, <c>BlobConnection</c> and <c>AllowedOrigins</c> to be
    /// configured before it will build a host — so generating a migration would depend on a
    /// developer's local secrets. Migrations are a schema operation and need no live database,
    /// so the connection string below is a placeholder: it selects the provider and is never
    /// opened. This is design-time only and is not part of the running application's
    /// composition.
    /// </remarks>
    internal sealed class TrailBlazeContextFactory : IDesignTimeDbContextFactory<TrailBlazeContext>
    {
        public TrailBlazeContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<TrailBlazeContext>()
                .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=TrailBlaze;Trusted_Connection=True;")
                .Options;

            return new TrailBlazeContext(options);
        }
    }
}
