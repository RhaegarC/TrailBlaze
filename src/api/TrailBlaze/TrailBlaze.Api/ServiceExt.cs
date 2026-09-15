using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Repository;
using TrailBlaze.Service;

namespace TrailBlaze.Api
{
    internal static class ServiceExt
    {
        /// <summary>Registers application services together with their dependencies. The
        /// DbContext and the repositories are registered by
        /// <see cref="PersistenceExtensions.AddRepositoryPersistence"/>, so it must run before
        /// the services that consume them.</summary>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="connectionString">Resolved by the composition root from configuration.</param>
        public static IServiceCollection RegistService(this IServiceCollection services, string? connectionString)
        {
            // Register persistence (DbContext + repositories)
            services.AddRepositoryPersistence(connectionString);

            // Register service
            services.AddScoped<IUserService, UserService>();

            // Others
            services.AddHttpContextAccessor();

            return services;
        }

        /// <summary>Registers Entra ID bearer-token authentication. The scheme is wired only
        /// when <see cref="Constant.ConfigKey.TenantId"/> and
        /// <see cref="Constant.ConfigKey.Audience"/> are both present, so a project that has
        /// not been pointed at a tenant still starts for /health and OpenAPI. Until then every
        /// request is anonymous — <c>Program</c> logs a warning at startup saying so.</summary>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="configuration">Resolved by the composition root from configuration.</param>
        public static IServiceCollection AddEntraAuthentication(
            this IServiceCollection services, IConfiguration configuration)
        {
            string? tenantId = configuration[Constant.ConfigKey.TenantId];
            string? audience = configuration[Constant.ConfigKey.Audience];

            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(audience))
            {
                // No scheme, but these still register what UseAuthentication and
                // UseAuthorization need in order to run.
                services.AddAuthentication();
                services.AddAuthorization();
                return services;
            }

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // v2.0 metadata also validates the issuer, so ValidateIssuer needs no
                    // separate ValidIssuer. No client secret: validating inbound tokens only
                    // needs the public signing keys that Authority serves.
                    options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
                    options.Audience = audience;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                    };
                });

            services.AddAuthorization();

            return services;
        }

        public static IServiceCollection AllowCORS(this IServiceCollection services, IConfiguration configuration)
        {
            string? originsStr = configuration[Constant.ConfigKey.AllowedOrigins];
            if (string.IsNullOrWhiteSpace(originsStr))
            {
                throw new InvalidOperationException(Constant.Message.NoAllowedOrigins);
            }

            string[] origins = originsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            services.AddCors(options =>
            {
                options.AddPolicy(Constant.App.CORSPolicyName,
                    builder => builder
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod());
            });
            return services;
        }
    }
}
