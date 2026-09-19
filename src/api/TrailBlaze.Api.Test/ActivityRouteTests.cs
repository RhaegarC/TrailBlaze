namespace TrailBlaze.Api.Test;

using System.Net;
using System.Text;

/// <summary>
/// What the activity routes answer a caller who has not signed in, and where they live.
/// </summary>
/// <remarks>
/// <para>
/// <b>Entra is configured here, and that is not incidental.</b> The host is allowed to start
/// with no tenant and no audience, and in that state there is no authentication scheme at all —
/// so an <c>[Authorize]</c> route answers <b>500</b>, because there is no scheme to issue a
/// challenge with. That is a defect of its own (see the debt register), and it means a 401 can
/// only be observed against a host that has a scheme to challenge with. Wiring a tenant and an
/// audience is what makes this tier answer the question the route is actually asked.
/// </para>
/// <para>
/// <b>A 401 here is two claims at once.</b> It is the authorization rule — no anonymous caller
/// reaches an activity route — and it is the route template: were <c>api/activities</c> spelled
/// differently in the controller, none of these would match and every one would be a 404. That
/// the path is asserted by asking for it is why each route is named here rather than inferred
/// from the attribute.
/// </para>
/// <para>
/// The list route is absent deliberately: reading activities back is feature 05's question, and
/// a route named here would be a route feature 04 has to implement to satisfy its own tests.
/// </para>
/// <para>
/// Nothing reaches a database. The factory's connection string names a port nothing listens on,
/// so a request that got past the challenge would fail rather than quietly succeed.
/// </para>
/// </remarks>
public sealed class ActivityRouteTests
{
    /// <summary>A plausible-looking id: "no such entry" is the honest answer had the caller a
    /// token, and the point here is that they get no further than the door.</summary>
    private const string SomeId = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task An_activity_route_turns_an_anonymous_caller_away(string method)
    {
        await using var factory = new TrailBlazeApiFactory(new()
        {
            ["TenantId"] = "00000000-0000-0000-0000-000000000000",
            ["Audience"] = "api://trailblaze-tests",
        });

        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.SendAsync(Request(method));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage Request(string method)
    {
        // The collection route is the one that takes no id, and it is asked for as written so a
        // template that moved is caught rather than assumed.
        string path = method == "POST" ? "/api/activities" : $"/api/activities/{SomeId}";
        var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        return request;
    }
}
