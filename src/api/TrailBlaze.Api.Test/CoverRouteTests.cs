namespace TrailBlaze.Api.Test;

using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TrailBlaze.Api.Controllers;

/// <summary>
/// Where the cover route lives, who it turns away, and what a caller is able to put in it.
/// </summary>
public sealed class CoverRouteTests
{
    private const string SomeId = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

    /// <summary>
    /// Closed like the rest of the activity surface: a cover is set by a signed-in person, and the
    /// route is the only place that can say so.
    /// </summary>
    [Fact]
    public async Task The_cover_route_turns_an_anonymous_caller_away()
    {
        await using TrailBlazeApiFactory factory = Configured();
        using HttpClient client = factory.CreateClient();

        using var body = new MultipartFormDataContent();
        body.Add(new StringContent("not-a-real-image"), "file", "cover.jpg");

        HttpResponseMessage response = await client.PostAsync($"/api/activity/{SomeId}/cover", body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The route accepts an id and a file, and nothing else — asserted on the action's own
    /// signature, because the absence is the rule: there is no parameter through which a caller
    /// could nominate a blob path or an existing item as the cover, so promoting private media to
    /// public is impossible by construction rather than by a check someone could remove.
    /// </summary>
    [Fact]
    public void The_cover_route_accepts_an_id_and_a_file_and_nothing_else()
    {
        ParameterInfo[] parameters = typeof(ActivityController)
            .GetMethod(nameof(ActivityController.UploadCover))!
            .GetParameters();

        Assert.Equal(["id", "file"], parameters.Select(parameter => parameter.Name));

        // The file is the form part itself rather than a model it binds into, so there is no type
        // between the request and the service for a media id or a path to arrive on either.
        Assert.Equal(typeof(IFormFile), parameters[1].ParameterType);
    }

    /// <summary>
    /// The same absence seen from the other side: what a client can put in the form part is a file
    /// and its contents, with no property through which a stored object could be named.
    /// </summary>
    [Fact]
    public void The_form_part_offers_no_way_to_name_a_stored_object()
    {
        ParameterInfo file = typeof(ActivityController)
            .GetMethod(nameof(ActivityController.UploadCover))!
            .GetParameters()[1];

        Assert.DoesNotContain(
            file.ParameterType.GetProperties(),
            property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Media", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Blob", StringComparison.OrdinalIgnoreCase));
    }

    private static TrailBlazeApiFactory Configured() => new(new()
    {
        ["TenantId"] = "00000000-0000-0000-0000-000000000000",
        ["Audience"] = "api://trailblaze-tests",
    });
}
