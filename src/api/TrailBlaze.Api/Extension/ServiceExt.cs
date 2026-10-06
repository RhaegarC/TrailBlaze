namespace TrailBlaze.Api.Extension;

using System.Globalization;
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
    /// <summary>Registers application services together with their dependencies. Must run after
    /// <see cref="PersistenceExtensions.AddRepositoryPersistence"/>, which registers the context
    /// and the repositories the services consume.</summary>
    public static IServiceCollection RegistService(
        this IServiceCollection services,
        IConfiguration configuration,
        string dbConnection,
        string blobConnection)
    {
        services.AddRepositoryPersistence(dbConnection);

        // A singleton: the client is thread-safe and holds a connection pool worth keeping.
        services.AddSingleton<IStorageRepository>(
            _ => new AzureBlobStorageRepository(blobConnection));

        // The window a media read URL is given. Optional: an absent or unparsable setting falls to the
        // policy's default, and a value above its cap is clamped there rather than honoured here.
        services.AddSingleton(_ => new SignedUrlLifetime(
                double.TryParse(
                    configuration[Constant.ConfigKey.MediaUrlTtlMinutes],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double minutes)
                    ? TimeSpan.FromMinutes(minutes)
                    : SignedUrlLifetime.Default));

        // The caps an upload is held to, read the same way: absent, unreadable or non-positive falls
        // to the default, and a value above the ceiling is clamped there rather than honoured here.
        services.AddSingleton(_ => new UploadSizeCaps(
            ParseBytes(configuration[Constant.ConfigKey.ImageUploadCapBytes]),
            ParseBytes(configuration[Constant.ConfigKey.VideoUploadCapBytes])));

        // The shape of an image's derivative, read the same way again.
        services.AddSingleton(_ => new ThumbnailOptions(
            ParseInt(configuration[Constant.ConfigKey.ThumbnailQuality]),
            ParseInt(configuration[Constant.ConfigKey.ThumbnailMaxDimension])));

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IActivityAuthorizationService, ActivityAuthorizationService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IUploadValidationService, UploadValidationService>();
        services.AddScoped<IThumbnailService, ThumbnailService>();
        services.AddScoped<IUserContextService, UserContextService>();

        services.AddEntraAuthentication(configuration);
        services.AllowCORS(configuration);
        services.AddHealthChecks();

        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>A configured byte count, or zero for an absent or unreadable one, which
    /// <see cref="UploadSizeCaps"/> reads as the absence of a setting.</summary>
    private static long ParseBytes(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long bytes)
            ? bytes
            : 0;

    /// <summary>A configured integer, or zero for an absent or unreadable one, which
    /// <see cref="ThumbnailOptions"/> reads as the absence of a setting.</summary>
    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
            ? number
            : 0;

    /// <summary>Reads a configuration value the application cannot run without, and names it in the
    /// failure.</summary>
    public static string RequireSetting(this IConfiguration configuration, string key, string message)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }

        return value;
    }

    /// <summary>Registers Entra ID bearer-token authentication. The scheme is wired only when the
    /// tenant and audience are both present, so a project that has not been pointed at a tenant still
    /// starts for /health and OpenAPI — with every request anonymous, which <c>Program</c> warns
    /// about at startup.</summary>
    public static IServiceCollection AddEntraAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        string? tenantId = configuration[Constant.ConfigKey.TenantId];
        string? audience = configuration[Constant.ConfigKey.Audience];

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(audience))
        {
            services.AddAuthentication();
            services.AddAuthorization();
            return services;
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // v2.0 metadata also validates the issuer, so ValidateIssuer needs no separate
                // ValidIssuer. No client secret: inbound validation needs only the public keys.
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
