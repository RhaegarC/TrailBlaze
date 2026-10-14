namespace TrailBlaze.Repository.Test.TestSupport;

using Azure.Core;

/// <summary>
/// An identity that cannot authenticate, so the failure an application has when its identity is
/// misconfigured is reachable without misconfiguring one.
/// </summary>
/// <remarks>
/// What is asserted through it is that the failure surfaces at all. A mint that swallowed this and
/// returned the blob's bare URL would return a string of the right shape that authorizes nothing —
/// the caller would store it, hand it out, and learn about it from a customer's broken image.
/// </remarks>
internal sealed class RefusingCredential : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No identity is available to sign as.");

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No identity is available to sign as.");
}
