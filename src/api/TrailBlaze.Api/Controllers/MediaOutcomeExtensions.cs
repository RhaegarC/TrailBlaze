namespace TrailBlaze.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using TrailBlaze.Model;
using TrailBlaze.Model.Media;

/// <summary>
/// Maps a <see cref="MediaOutcome"/> onto HTTP.
/// </summary>
/// <remarks>
/// Shared rather than private to one controller, because two of them answer with this outcome:
/// upload and listing sit on the activity's route and deletion on the media one, and a switch copied
/// between them is a status code that changes in one place and not the other. Both controllers keep
/// their own mapper for <c>ActivityOutcome</c> and <c>ProfileOutcome</c>, which only one each
/// produces — this is the case that differs, not a change of policy.
/// </remarks>
internal static class MediaOutcomeExtensions
{
    /// <summary>
    /// The status code and body each outcome becomes.
    /// </summary>
    /// <remarks>
    /// <c>ValidationProblem</c> rather than a bare <c>BadRequest</c>, so a refused upload arrives in
    /// the shape <c>[ApiController]</c> produces for a body that did not bind, matching the avatar
    /// route. The 409 carries the limit as a detail string rather than a field-keyed error, because
    /// no single field caused it.
    /// </remarks>
    /// <param name="controller">The controller responding.</param>
    /// <param name="outcome">What the service reported.</param>
    /// <returns>The action result.</returns>
    public static IActionResult ToActionResult(this ControllerBase controller, MediaOutcome outcome) =>
        outcome.Kind switch
        {
            // 201 with no Location header: there is no route that reads one item back, and inventing
            // one so the header has somewhere to point would be a route nobody asked for.
            MediaOutcomeKind.Uploaded => controller.StatusCode(
                StatusCodes.Status201Created, outcome.Item),
            MediaOutcomeKind.Deleted => controller.NoContent(),
            MediaOutcomeKind.NotFound => controller.NotFound(),
            MediaOutcomeKind.NoCaller => controller.Unauthorized(),
            MediaOutcomeKind.Forbidden => controller.Forbid(),
            MediaOutcomeKind.LimitReached => controller.Problem(
                detail: Constant.Message.MediaLimitReached,
                statusCode: StatusCodes.Status409Conflict),
            _ => controller.ValidationProblem(
                new ValidationProblemDetails(new Dictionary<string, string[]>(outcome.Errors!))),
        };
}
