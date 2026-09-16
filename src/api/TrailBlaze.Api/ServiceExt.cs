namespace TrailBlaze.Api;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Repository;
using TrailBlaze.Service;

internal static class ServiceExt
{
    /// <summary>Registers application services together with their dependencies. The
    /// DbContext and the repositories are registered by
    /// <see cref="PersistenceExtensions.AddRepositoryPersistence"/>, so it must run before
    /// the services that consume them.</summary>
    /// <remarks>
    /// Everything the application registers is registered here, so <c>Program</c> reads
    /// configuration and calls this, and does not compose on its own. The two connection
    /// strings arrive resolved; Entra ID and CORS read their own values from
    /// <paramref name="configuration"/> inside their extension methods.
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">The host configuration, passed to the extension methods
    /// that resolve their own settings.</param>
    /// <param name="dbConnection">Resolved by the composition root from configuration.</param>
    /// <param name="blobConnection">Resolved by the composition root from configuration.</param>
    public static IServiceCollection RegistService(
        this IServiceCollection services,
        IConfiguration configuration,
        string dbConnection,
        string blobConnection)
    {
        // Register persistence (DbContext + repositories)
        services.AddRepositoryPersistence(dbConnection);

        // Register storage. A singleton, deliberately: the underlying client is thread-safe
        // and holds a connection pool, so building one per request would throw that away and
        // add a client construction to every media call.
        services.AddSingleton<IStorageRepository>(
            _ => new AzureBlobStorageRepository(blobConnection));

        // Register service
        services.AddScoped<IUserService, UserService>();

        // Stateless, but registered rather than static so the upload rules have one home the
        // routes depend on through injection: if a cap ever needs to come from configuration
        // rather than Constant, that changes here and no caller changes at all.
        services.AddSingleton<IUploadValidationService, UploadValidationService>();

        // Register the caller abstraction. Scoped, because it reads the current request's
        // claims; nothing outside a request should resolve it.
        services.AddScoped<IUserContextService, UserContextService>();

        // Register authentication, CORS and health checks. Each resolves its own
        // configuration, so none of them needs a value passed in.
        services.AddEntraAuthentication(configuration);
        services.AllowCORS(configuration);
        services.AddHealthChecks();

        // Others
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>Reads a configuration value that the application cannot run without, and
    /// names it in the failure.</summary>
    /// <remarks>
    /// The alternative — accepting an empty value and discovering it on the first request
    /// that needs it — turns a misconfiguration into a runtime error far from its cause.
    /// A missing setting is a deployment mistake, and a deployment mistake should surface at
    /// startup or not at all.
    /// </remarks>
    /// <param name="configuration">Configuration to read from.</param>
    /// <param name="key">The flat key to require, from <see cref="Constant.ConfigKey"/>.</param>
    /// <param name="message">The message to fail with; it names the key.</param>
    /// <returns>The configured value, never blank.</returns>
    /// <exception cref="InvalidOperationException">The value is missing or blank.</exception>
    public static string RequireSetting(this IConfiguration configuration, string key, string message)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }

        return value;
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
        string originsStr = configuration.RequireSetting(
            Constant.ConfigKey.AllowedOrigins, Constant.Message.NoAllowedOrigins);

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
