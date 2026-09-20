namespace TrailBlaze.Api.Extension;

using Microsoft.AspNetCore.Mvc;
using TrailBlaze.Model.Activity;

/// <summary>
/// Maps a <see cref="CoverOutcome"/> onto HTTP.
/// </summary>
internal static class CoverOutcomeExtensions
{
    /// <summary>
    /// The status code and body each outcome becomes.
    /// </summary>
    /// <remarks>
    /// <c>Ok</c> rather than the media route's 201: the cover is a field of an activity that already
    /// exists, so an upload that replaced one created nothing. <c>ValidationProblem</c> rather than
    /// a bare <c>BadRequest</c>, so a refused image arrives in the shape <c>[ApiController]</c>
    /// produces for a body that did not bind.
    /// </remarks>
    public static IActionResult ToActionResult(this ControllerBase controller, CoverOutcome outcome) =>
        outcome.Kind switch
        {
            CoverOutcomeKind.Uploaded => controller.Ok(outcome.Cover),
            CoverOutcomeKind.NotFound => controller.NotFound(),
            CoverOutcomeKind.NoCaller => controller.Unauthorized(),
            _ => controller.ValidationProblem(
                new ValidationProblemDetails(new Dictionary<string, string[]>(outcome.Errors!))),
        };
}
