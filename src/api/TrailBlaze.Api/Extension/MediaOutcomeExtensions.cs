namespace TrailBlaze.Api.Extension;

using Microsoft.AspNetCore.Mvc;
using TrailBlaze.Model;
using TrailBlaze.Model.Media;

/// <summary>
/// Maps a <see cref="MediaOutcome"/> onto HTTP.
/// </summary>
internal static class MediaOutcomeExtensions
{
    /// <summary>
    /// The status code and body each outcome becomes.
    /// </summary>
    /// <remarks>
    /// <c>ValidationProblem</c> rather than a bare <c>BadRequest</c>, so a refused upload arrives in
    /// the shape <c>[ApiController]</c> produces for a body that did not bind. The 409 carries the
    /// limit as a detail string rather than a field-keyed error, because no single field caused it.
    /// </remarks>
    public static IActionResult ToActionResult(this ControllerBase controller, MediaOutcome outcome) =>
        outcome.Kind switch
        {
            // 201 with no Location header: there is no route that reads one item back.
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

    /// <summary>
    /// The status code and body minting a read URL becomes.
    /// </summary>
    /// <remarks>
    /// No forbidden case: being able to see an item is being able to fetch it, so the only refusal is
    /// not-found — which covers an item this caller may not see, because whether it exists at all is
    /// the fact being withheld.
    /// </remarks>
    public static IActionResult ToActionResult(this ControllerBase controller, MediaUrlOutcome outcome) =>
        outcome.Kind switch
        {
            MediaUrlOutcomeKind.Minted => controller.Ok(outcome.Url),
            MediaUrlOutcomeKind.NoCaller => controller.Unauthorized(),
            _ => controller.NotFound(),
        };
}
