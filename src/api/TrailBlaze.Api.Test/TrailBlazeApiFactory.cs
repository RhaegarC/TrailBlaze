namespace TrailBlaze.Api.Test;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// Boots the real application pipeline for tests.
/// </summary>
/// <remarks>
/// <para>
/// The application refuses to start without its required settings, so the factory supplies
/// them. Its connection strings point at a port nothing listens on, because this tier asserts
/// how the host behaves rather than what the database holds: none of these tests should reach
/// a server.
/// </para>
/// <para>
/// <b>That is a statement about this tier, not about the suite.</b> The unreachable string also
/// appears in <c>TrailBlaze.Repository.Test</c>'s <c>OfflineContext</c>, and there it marks the
/// model-metadata tests, which need a context and no connection either. The repository tier's
/// container tests do reach a server — through their own fixtures, against the databases the
/// compose file starts — so a reader who finds this string in more than one place should not
/// conclude that no test anywhere talks to a database.
/// </para>
/// <para>
/// Nothing has to be removed from the service collection to keep this tier offline. Migrations
/// are applied by the deployment pipeline rather than at startup, and nothing else runs at boot,
/// so the context is resolved lazily and not until a request needs it.
/// </para>
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
    }
}
