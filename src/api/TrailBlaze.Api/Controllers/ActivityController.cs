namespace TrailBlaze.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model.Activity;

/// <summary>
/// One activity: create it, read it, edit it, remove it.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>[Authorize]</c> is on the type, not on each action</b>, for the reason the user
/// controller gives: a rule declared once cannot be forgotten by the next route added.
/// </para>
/// <para>
/// <b>No route asks who is calling.</b> Ownership is decided in the service from the validated
/// token and is feature 09's subject; adding the routes now would be a check here that a later
/// feature has to move rather than a rule this one keeps.
/// </para>
/// <para>
/// The list route is not here: reading many activities, and deciding which of them a caller may
/// see, is feature 05's rule and is where its pagination and ordering belong.
/// </para>
/// </remarks>
[ApiController]
[Authorize]
[Route("api/activities")]
public class ActivityController(IActivityService activityService) : Controller
{
    private readonly IActivityService _activityService = activityService;

    /// <summary>Stores a new activity, attributed to the caller.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateActivityRequest request)
    {
        ActivityOutcome outcome = await _activityService.CreateAsync(request);

        if (outcome.Kind != ActivityOutcomeKind.Completed)
        {
            return Respond(outcome);
        }

        // The Location header names the route that reads it back, so a client that just
        // created an entry does not have to construct the address for it.
        return CreatedAtAction(nameof(Get), new { id = outcome.Activity!.Id }, outcome.Activity);
    }

    /// <summary>Reads one activity.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        ActivityOutcome outcome = await _activityService.GetAsync(id);

        return Respond(outcome);
    }

    /// <summary>Replaces the editable fields, including the type.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateActivityRequest request)
    {
        ActivityOutcome outcome = await _activityService.UpdateAsync(id, request);

        return Respond(outcome);
    }

    /// <summary>Soft-deletes an activity. The row is retained; every read stops seeing it.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        ActivityOutcome outcome = await _activityService.DeleteAsync(id);

        return Respond(outcome);
    }

    /// <summary>
    /// Maps a service outcome onto HTTP, which is all this layer adds to it: the service
    /// reports what happened and knows nothing about status codes.
    /// </summary>
    /// <remarks>
    /// <c>ValidationProblem</c> rather than a bare <c>BadRequest</c>, so the field-keyed errors
    /// arrive in the same shape <c>[ApiController]</c> produces for a body that could not be
    /// bound at all — one error format for a client rather than two.
    /// </remarks>
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
