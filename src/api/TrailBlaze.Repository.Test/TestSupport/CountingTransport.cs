namespace TrailBlaze.Repository.Test.TestSupport;

using System.Net.Http;

/// <summary>
/// Counts the delegation-key requests that actually leave the process, and passes everything
/// through untouched.
/// </summary>
/// <remarks>
/// <para>
/// <b>The count has to be taken here rather than at the credential.</b> The obvious instrument —
/// a <c>TokenCredential</c> that tallies the tokens it hands out — cannot make this claim, because
/// the pipeline caches the token it was given and consults the credential once for the life of the
/// client however many key fetches happen behind it. A credential counter therefore reports one
/// whether the key is cached or not, which is the one thing the test exists to tell apart.
/// </para>
/// <para>
/// What is counted is the operation on the wire, and nothing is simulated: the request goes on to
/// the emulator and the response comes back from it. The tier's rule that no double stands in for
/// the store is kept — this observes real traffic rather than answering in its place.
/// </para>
/// </remarks>
internal sealed class CountingTransport(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    private const string DelegationKeyOperation = "comp=userdelegationkey";

    private int _keyRequests;

    /// <summary>How many user-delegation-key requests have gone out.</summary>
    public int DelegationKeyRequests => Volatile.Read(ref _keyRequests);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri?.Query.Contains(DelegationKeyOperation, StringComparison.OrdinalIgnoreCase) == true)
        {
            Interlocked.Increment(ref _keyRequests);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
