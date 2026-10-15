namespace TrailBlaze.Repository.Test.TestSupport;

using System.Net.Security;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;

/// <summary>
/// A transport that trusts a certificate it cannot verify — for a loopback endpoint, and nowhere
/// else.
/// </summary>
/// <remarks>
/// <para>
/// The emulator serves TLS with a certificate generated when its container starts, so nothing
/// outside that container has a reason to trust it and no CA ever signed it. It has to serve TLS at
/// all because the emulator refuses an OAuth-authenticated request over plain HTTP.
/// </para>
/// <para>
/// <b>The exemption is scoped to loopback rather than to the transport.</b> The same tier runs
/// against a real account when <c>TRAILBLAZE_STORAGE_CONNECTION</c> names one, and a client that
/// accepted any certificate would — in that configuration — be accepting a forged one from the
/// network. Asking the request where it is going is what keeps the shortcut from travelling with
/// the code: a real endpoint's certificate is validated as normal, and only the certificate of a
/// server on this machine is taken on faith.
/// </para>
/// </remarks>
internal static class LoopbackTransport
{
    /// <summary>An <see cref="HttpClient"/> that reaches the emulator and validates anything
    /// else.</summary>
    public static HttpClient Client() => new(Handler());

    /// <summary>Client options for the Azure SDK built over <see cref="Handler"/>.</summary>
    public static BlobClientOptions Options() => new() { Transport = new HttpClientTransport(Handler()) };

    /// <summary>Client options for a caller that has wrapped <see cref="Handler()"/> in something of
    /// its own — the trust above comes from that handler, not from here.</summary>
    public static BlobClientOptions Options(HttpMessageHandler inner) => new()
    {
        Transport = new HttpClientTransport(inner),
    };

    /// <summary>The transport the options above are built over, exposed so a caller can put a
    /// wrapper in front of it.</summary>
    public static HttpMessageHandler Handler() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
            errors == SslPolicyErrors.None || request.RequestUri?.IsLoopback == true,
    };
}
