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
/// are applied by the deployment pipeline rather than at startup, so the context is resolved
/// lazily and not until a request needs it.
/// </para>
/// <para>
/// <b>One thing does reach for the database at startup, and it is why this string is still the
/// right one.</b> Feature 03's admin seeder runs as a hosted service, so booting the host makes
/// one attempt at the connection below — and it fails, every time, in milliseconds, because
/// nothing listens on port 1. That is deliberate rather than tolerated: the seeder's failures
/// are logged instead of fatal, so this factory is at once the tier's offline guarantee and the
/// regression test for it. If a change ever makes a missing database stop the host, every test
/// in this project goes red at once, which is the loudest available signal.
/// </para>
/// <para>
/// The admin values are supplied for a different reason: absent admin configuration <em>is</em>
/// fatal by design, so a factory that omitted them would fail to boot for a reason unrelated to
/// whatever its test is about.
/// </para>
/// </remarks>
internal sealed class TrailBlazeApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Port 1 on loopback — nothing listens, so nothing connects.</summary>
    internal const string UnreachableConnection =
        "Server=127.0.0.1,1;Database=TrailBlaze;User Id=sa;"
        + "Password=NotARealPassword!1;TrustServerCertificate=True;Connect Timeout=1";

    /// <summary>The administrator this factory configures: a well-formed Entra object id, so
    /// nothing on the seeder's own path has cause to object to it.</summary>
    internal const string AdminObjectId = "00000000-0000-0000-0000-0000000000ad";

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
            ["AdminObjectId"] = AdminObjectId,
            ["AdminDisplayName"] = "TrailBlaze Test Admin",
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
