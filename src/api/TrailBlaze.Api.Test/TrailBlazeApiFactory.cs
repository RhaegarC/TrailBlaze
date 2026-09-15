namespace TrailBlaze.Api.Test
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Boots the real application pipeline for tests.
    /// </summary>
    /// <remarks>
    /// The application refuses to start without its required settings, so the factory supplies
    /// them. The connection strings point at a port nothing listens on: this tier asserts how
    /// the host behaves, not what the database holds, and no test here should reach a server.
    /// </remarks>
    internal sealed class TrailBlazeApiFactory : WebApplicationFactory<Program>
    {
        /// <summary>Port 1 on loopback — nothing listens, so nothing connects.</summary>
        internal const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=TrailBlaze;User Id=sa;"
            + "Password=NotARealPassword!1;TrustServerCertificate=True;Connect Timeout=1";

        private readonly Dictionary<string, string?> _settings;

        /// <param name="settings">Overrides applied on top of the defaults. Setting a value to
        /// null or blank is how a test asks what the host does without it.</param>
        public TrailBlazeApiFactory(Dictionary<string, string?>? settings = null)
        {
            _settings = new Dictionary<string, string?>
            {
                ["DbConnection"] = UnreachableConnection,
                ["BlobConnection"] = $"DefaultEndpointsProtocol=https;AccountName=trailblazetests;AccountKey={Convert.ToBase64String(new byte[32])};EndpointSuffix=core.windows.net",
                ["AllowedOrigins"] = "http://localhost",
            };

            foreach ((string key, string? value) in settings ?? [])
            {
                _settings[key] = value;
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            foreach ((string key, string? value) in _settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services =>
            {
                // Migrations run as a hosted service so they can be removed — there is no
                // database here to migrate. This is the seam described on
                // DatabaseMigrationService: production keeps the unconditional startup path.
                //
                // Removed by implementation type, not by `RemoveAll<IHostedService>()`: that
                // would also drop every hosted service added later, and this tier would go on
                // passing while never starting them.
                ServiceDescriptor? migrations = services.FirstOrDefault(
                    descriptor => descriptor.ImplementationType == typeof(DatabaseMigrationService));

                if (migrations is not null)
                {
                    services.Remove(migrations);
                }
            });
        }
    }
}
