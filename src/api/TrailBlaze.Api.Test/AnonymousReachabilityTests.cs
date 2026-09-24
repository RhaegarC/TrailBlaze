namespace TrailBlaze.Api.Test;

using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The anonymous surface, asserted as a closed list of two rather than as the absence of a check.
/// </summary>
/// <remarks>
/// Default deny is a property of the endpoint table, so it is read from the endpoint table: every
/// controller action either carries an explicit anonymous grant or requires authorization, and the
/// grants are exactly the two read routes. A controller added later without <c>[Authorize]</c>
/// fails here rather than answering 200 to the internet, which no status-code test could catch
/// without enumerating the routes by hand.
/// </remarks>
public sealed class AnonymousReachabilityTests
{
    /// <summary>What an anonymous caller may reach, and nothing else.</summary>
    private static readonly string[] OpenByDesign =
        ["GET api/Activity", "GET api/Activity/{id}"];

    [Fact]
    public void The_routes_an_anonymous_caller_may_reach_are_exactly_two()
    {
        using TrailBlazeApiFactory factory = Configured();

        string[] open = [.. ControllerActions(factory)
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Name)
            .Order()];

        Assert.Equal(OpenByDesign, open);
    }

    /// <summary>
    /// The other half of default deny: authorization is inherited from the controller, so a new
    /// controller that forgot its attribute is reachable by anyone and would not appear in the list
    /// above at all.
    /// </summary>
    [Fact]
    public void No_action_is_reachable_without_a_grant_or_an_authorization_attribute()
    {
        using TrailBlazeApiFactory factory = Configured();

        string[] ungated = [.. ControllerActions(factory)
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(Name)
            .Order()];

        Assert.Empty(ungated);
    }

    /// <summary>
    /// The probe is outside the API surface and stays so: it answers a load balancer that holds no
    /// token, and it reaches nothing.
    /// </summary>
    [Fact]
    public async Task The_health_route_answers_an_anonymous_caller()
    {
        using TrailBlazeApiFactory factory = Configured();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// A host with Entra wired up. Needed even to enumerate routes: the composition root warns
    /// rather than fails without it, but a test that asserted against the unconfigured wiring would
    /// be describing a host this application never runs.
    /// </summary>
    private static TrailBlazeApiFactory Configured() => new(new()
    {
        ["TenantId"] = "00000000-0000-0000-0000-000000000000",
        ["Audience"] = "api://trailblaze-tests",
    });

    private static IEnumerable<Endpoint> ControllerActions(TrailBlazeApiFactory factory) =>
        factory.Services
            .GetRequiredService<IEnumerable<EndpointDataSource>>()
            .SelectMany(source => source.Endpoints)
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null);

    /// <summary>The verb and the template, so a route that moved fails here rather than only in the
    /// test that calls it.</summary>
    private static string Name(Endpoint endpoint) =>
        $"{string.Join(",", endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} "
        + (endpoint as RouteEndpoint)?.RoutePattern.RawText;
}
