namespace TrailBlaze.Api.Test;

using System.Net;
using System.Text;

/// <summary>
/// What the activity routes answer a caller who has not signed in, and where they live.
/// </summary>
public sealed class ActivityRouteTests
{
    private const string SomeId = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task A_protected_activity_route_turns_an_anonymous_caller_away(string method)
    {
        await using TrailBlazeApiFactory factory = Configured();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.SendAsync(Request(method));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_list_route_lets_an_anonymous_caller_past_the_door()
    {
        await using TrailBlazeApiFactory factory = Configured();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/activity");

        // 500 is the assertion: the request reached the service and failed on the factory's
        // unreachable store. A 401 would mean the route is not anonymous and a 404 that no such
        // route exists, and this tier has neither a database nor a token to tell those apart
        // any other way.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private static HttpRequestMessage Request(string method)
    {
        // The collection route is the one that takes no id, and it is asked for as written so a
        // template that moved is caught rather than assumed.
        string path = method == "POST" ? "/api/activity" : $"/api/activity/{SomeId}";
        var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        return request;
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
