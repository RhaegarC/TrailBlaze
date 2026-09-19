namespace TrailBlaze.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model.Activity;

/// <summary>
/// One activity: page through them, create it, read it, edit it, remove it.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ActivityController(IActivityService activityService) : ControllerBase
{
    private readonly IActivityService _activityService = activityService;

    /// <summary>
    /// One page of the activities the caller may read, newest first.
    /// </summary>
    /// <remarks>
    /// The one route here reachable without a token; the visibility filter is what makes that safe.
    /// </remarks>
    /// <param name="page">Zero-based page index. Negative is read as the first page.</param>
    /// <param name="pageSize">Rows per page. Defaults to 10, and is clamped to 100.</param>
    /// <returns>The page, and the size of everything the caller may read.</returns>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List(int page = 0, int pageSize = 0)
    {
        ActivityPage result = await _activityService.GetPageAsync(page, pageSize);

        return Ok(result);
    }

    /// <summary>
    /// Stores a new activity, attributed to the caller.
    /// </summary>
    /// <param name="request">The fields to store.</param>
    /// <returns>The stored activity, the reasons the input was refused, or no caller.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateActivityRequest request)
    {
        ActivityOutcome outcome = await _activityService.CreateAsync(request);

        if (outcome.Kind != ActivityOutcomeKind.Completed)
        {
            return Respond(outcome);
        }

        // CreatedAtAction, so the Location header names the route that reads the entry back.
        return CreatedAtAction(nameof(Get), new { id = outcome.Activity!.Id }, outcome.Activity);
    }

    /// <summary>
    /// Reads one activity.
    /// </summary>
    /// <param name="id">The activity's id.</param>
    /// <returns>The activity, or not-found for an id that is unknown or already deleted.</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        ActivityOutcome outcome = await _activityService.GetAsync(id);

        return Respond(outcome);
    }

    /// <summary>
    /// Replaces the editable fields, including the type.
    /// </summary>
    /// <param name="id">The activity's id.</param>
    /// <param name="request">The fields to store.</param>
    /// <returns>The updated activity, the reasons the input was refused, or not-found.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateActivityRequest request)
    {
        ActivityOutcome outcome = await _activityService.UpdateAsync(id, request);

        return Respond(outcome);
    }

    /// <summary>
    /// Soft-deletes an activity. The row is retained; every read stops seeing it.
    /// </summary>
    /// <param name="id">The activity's id.</param>
    /// <returns>No content, or not-found for an id that is unknown or already deleted.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        ActivityOutcome outcome = await _activityService.DeleteAsync(id);

        return Respond(outcome);
    }

    /// <summary>
    /// Maps a service outcome onto HTTP.
    /// </summary>
    /// <remarks>
    /// <c>ValidationProblem</c> rather than a bare <c>BadRequest</c>, so the field-keyed errors
    /// arrive in the shape <c>[ApiController]</c> produces for a body that did not bind at all.
    /// </remarks>
    /// <param name="outcome"></param>
    /// <returns></returns>
    private IActionResult Respond(ActivityOutcome outcome) => outcome.Kind switch
    {
        ActivityOutcomeKind.Completed => Ok(outcome.Activity),
        ActivityOutcomeKind.Deleted => NoContent(),
        ActivityOutcomeKind.NotFound => NotFound(),
        ActivityOutcomeKind.NoCaller => Unauthorized(),
        _ => ValidationProblem(
            new ValidationProblemDetails(new Dictionary<string, string[]>(outcome.Errors!))),
    };
}
