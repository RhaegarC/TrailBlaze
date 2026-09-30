namespace TrailBlaze.Api.Test;

using System.Net;

/// <summary>
/// Where the media routes live, and what they answer a caller who has not signed in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The read routes admit an anonymous caller; the write routes do not.</b> A read is gated by the
/// activity's <c>Type</c>, so a <c>Public</c> entry's media is reachable without a token and the
/// route has to let the request through before that judgement can be made (Decision #2, reversed
/// 2026-09-30). Upload and delete still ask for a sign-in, and the service refuses a nameless caller
/// for both as well — the guard and the attribute are two halves of one rule there.
/// </para>
/// <para>
/// <b>What this tier cannot reach, said plainly.</b> These tests assert admission, not answers: with
/// no token and the factory's unreachable store, a request that gets past the door dies on the
/// connection, so 500 is the whole of the claim and 200, 404 and 403 are all outside this tier's
/// reach. Those live in <c>TrailBlaze.Service.Test</c>, against the service directly, and in the
/// feature 11 walkthrough.
/// </para>
/// <para>
/// <b>The read-URL route's storage count is placed accordingly.</b> Feature 07 asks that a refusal
/// reach the caller <em>before any storage call</em>, and the recording double that counts those
/// calls lives in <c>TrailBlaze.Service.Test</c> — not here. It has to: a count taken here would
/// hold because of the framework's ordering rather than because of anything this code does, and
/// such a test could not fail if the mint were moved above the gate, which is the one mistake it
/// would exist to catch.
/// </para>
/// </remarks>
public sealed class MediaRouteTests
{
    private const string SomeId = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

    [Theory]
    [InlineData("POST", "/api/activity/{0}/media")]
    [InlineData("DELETE", "/api/media/{0}")]
    public async Task A_media_mutation_route_turns_an_anonymous_caller_away(string method, string template)
    {
        await using TrailBlazeApiFactory factory = Configured();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), string.Format(template, SomeId)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/activity/{0}/media")]
    [InlineData("GET", "/api/media/{0}/url")]
    public async Task A_media_read_route_lets_an_anonymous_caller_past_the_door(string method, string template)
    {
        await using TrailBlazeApiFactory factory = Configured();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), string.Format(template, SomeId)));

        // 500 is the assertion: the request reached the service and failed on the factory's
        // unreachable store. A 401 would mean the route is not anonymous and a 404 that no such
        // route exists, and this tier has neither a database nor a token to tell those apart any
        // other way.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
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
