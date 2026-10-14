namespace TrailBlaze.Repository.Test.TestSupport;

using System.Text;
using Azure.Core;

/// <summary>
/// The identity the emulator accepts.
/// </summary>
/// <remarks>
/// Azurite run with <c>--oauth basic</c> decodes the bearer token and reads its claims without
/// verifying the signature — which is a documented property of the emulator, not a hole being
/// exploited: there is no key to verify against, and the account it opens is the one this machine
/// started. So a well-formed unsigned JWT carrying the claims it looks at is enough to be
/// authenticated, and that is what lets the delegation path be exercised with no tenant and no
/// credential.
/// </remarks>
internal sealed class EmulatorIdentity : TokenCredential
{
    /// <summary>Long-lived: the emulator reads the claims, and a token that lapsed mid-run would
    /// be a failure about this class rather than about the code under test.</summary>
    private readonly AccessToken _token = new(SignedPayload(), DateTimeOffset.MaxValue.AddDays(-1));

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        _token;

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        new(_token);

    /// <summary>An unsigned JWT — three dot-separated base64url parts, the last a placeholder.</summary>
    private static string SignedPayload()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        string header = """{"alg":"HS256","typ":"JWT"}""";

        // Spread over lines rather than joined with backslash continuations: a raw string literal
        // takes a backslash literally, so a continuation would put one in the JSON and the emulator
        // would refuse a token it could not decode. Newlines between tokens are valid JSON.
        string payload = $$"""
            {
              "aud": "https://storage.azure.com/",
              "iss": "https://sts.windows.net/trailblaze-test/",
              "iat": {{now}},
              "nbf": {{now - 60}},
              "exp": {{now + 3600}},
              "oid": "11111111-1111-1111-1111-111111111111",
              "tid": "22222222-2222-2222-2222-222222222222",
              "sub": "trailblaze-tests",
              "name": "trailblaze-tests"
            }
            """;

        return $"{Part(header)}.{Part(payload)}.unsigned";
    }

    private static string Part(string json) => Convert
        .ToBase64String(Encoding.UTF8.GetBytes(json))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
