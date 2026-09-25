namespace TrailBlaze.Api.Test;

using System.Net;

/// <summary>
/// Where the media routes live, and what they answer a caller who has not signed in.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this tier cannot reach, said plainly.</b> Every media route carries
/// <c>[Authorize]</c>, so without a token the actions are never entered: the multipart refusal for a
/// body carrying no file, and everything the service then decides, are outside this tier's reach.
/// That is a statement about the fixtures here rather than about the rules — the decisions are
/// asserted in <c>TrailBlaze.Service.Test</c> against the service directly, and the authenticated
/// surface wants a token this tier does not yet have.
/// </para>
/// <para>
/// <b>The read-URL route does not change that, and the claim it makes is placed accordingly.</b>
/// Feature 07 asks that the refusal reach the caller <em>before any storage call</em>, and the
/// recording double that counts those calls lives in <c>TrailBlaze.Service.Test</c> — not here.
/// It has to: with no token the request is refused by the authentication middleware, so the action
/// never runs and a zero-interaction assertion here would hold because of the framework's ordering
/// rather than because of anything this code does. Such a test could not fail if the mint were moved
/// above the gate, which is the one mistake it would exist to catch.
/// </para>
/// </remarks>
public sealed class MediaRouteTests
{
    private const string SomeId = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

    [Theory]
    [InlineData("POST", "/api/activity/{0}/media")]
    [InlineData("GET", "/api/activity/{0}/media")]
    [InlineData("DELETE", "/api/media/{0}")]
    [InlineData("GET", "/api/media/{0}/url")]
    public async Task A_media_route_turns_an_anonymous_caller_away(string method, string template)
    {
        await using TrailBlazeApiFactory factory = Configured();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), string.Format(template, SomeId)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A host with Entra wired up, which is what a 401 needs: with no scheme there is nothing to
    /// issue a challenge with, and an <c>[Authorize]</c> route answers 500 instead.
    /// </summary>
    private static TrailBlazeApiFactory Configured() => new(new()
    {
        ["TenantId"] = "00000000-0000-0000-0000-000000000000",
        ["Audience"] = "api://trailblaze-tests",
    });
}
