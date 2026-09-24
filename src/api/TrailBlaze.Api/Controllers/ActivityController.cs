namespace TrailBlaze.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrailBlaze.Api.Extension;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.Activity;
using TrailBlaze.Model.Media;

/// <summary>
/// One activity: page through them, create it, read it, edit it, remove it, and see what media it
/// carries.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ActivityController(
    IActivityService activityService,
    IMediaService mediaService) : ControllerBase
{
    private readonly IActivityService _activityService = activityService;
    private readonly IMediaService _mediaService = mediaService;

    /// <summary>
    /// One page of the activities the caller may read, newest first.
    /// </summary>
    /// <param name="page">Zero-based page index. Negative is read as the first page.</param>
    /// <param name="pageSize">Rows per page. Defaults to 10, and is clamped to 100.</param>
    /// <returns>The page, and the size of everything the caller may read.</returns>
    /// <remarks>The one route here reachable without a token; the visibility filter is what makes
    /// that safe.</remarks>
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
    /// <returns>The activity, or not-found for an id that is unknown, already deleted, or not
    /// readable by this caller.</returns>
    /// <remarks>Reachable without a token; what the caller may not read is answered 404 by the
    /// service, so the route admits the request before that judgement is made.</remarks>
    [AllowAnonymous]
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
    /// Soft-deletes an activity and its media. The row is retained; every read stops seeing it.
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
    /// Adds one image or video to an activity, to any signed-in caller who can read it
    /// (Decision #27).
    /// </summary>
    /// <remarks>
    /// Both limits are raised for this route deliberately: a 200 MB video exceeds Kestrel's own 30 MB
    /// body default and the form parser's 128 MB multipart default, either of which would refuse the
    /// upload as a bare 413 before the service could answer with the documented 400.
    /// </remarks>
    /// <param name="activityId">The activity's id.</param>
    /// <param name="file">The image or video.</param>
    /// <returns>The stored item, the reasons the upload was refused, or not-found.</returns>
    [HttpPost("{activityId}/media")]
    [RequestSizeLimit(Constant.Upload.MaxMediaRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = Constant.Upload.MaxMediaRequestBytes)]
    public async Task<IActionResult> UploadMedia(string activityId, [FromForm] IFormFile? file)
    {
        if (file is null)
        {
            // Without this the route would dereference a null and 500. A body with no file part is a
            // malformed request, which is a 400.
            return BadRequest(Constant.Message.NoFileUploaded);
        }

        await using Stream content = file.OpenReadStream();

        MediaOutcome outcome = await _mediaService.UploadAsync(
            activityId, content, file.ContentType, file.Length, file.FileName);

        return this.ToActionResult(outcome);
    }

    /// <summary>
    /// Uploads an activity's cover image, replacing any it already has.
    /// </summary>
    /// <remarks>
    /// No request-size override, unlike the media route: the 10 MB image cap sits below both
    /// Kestrel's 30 MB body default and the form parser's 128 MB multipart default, so an oversize
    /// file reaches the service and is answered with the reason a client can act on rather than a
    /// bare 413.
    /// </remarks>
    /// <param name="activityId">The activity's id.</param>
    /// <param name="file">The image.</param>
    /// <returns>The cover's URL, the reasons the upload was refused, or not-found.</returns>
    [HttpPost("{activityId}/cover")]
    public async Task<IActionResult> UploadCover(string activityId, [FromForm] IFormFile? file)
    {
        if (file is null)
        {
            // Without this the route would dereference a null and 500. A body with no file part is a
            // malformed request, which is a 400.
            return BadRequest(Constant.Message.NoFileUploaded);
        }

        await using Stream content = file.OpenReadStream();

        CoverOutcome outcome = await _activityService.UploadCoverAsync(
            activityId, content, file.ContentType, file.Length);

        return this.ToActionResult(outcome);
    }

    /// <summary>
    /// The metadata of every item an activity carries, oldest first.
    /// </summary>
    /// <param name="activityId">The activity's id.</param>
    /// <returns>The items; not-found for an activity the caller may not read.</returns>
    [HttpGet("{activityId}/media")]
    public async Task<IActionResult> ListMedia(string activityId)
    {
        MediaListing listing = await _mediaService.ListAsync(activityId);

        return listing.Found ? Ok(listing.Items) : NotFound();
    }

    /// <summary>
    /// Removes an item: its blob and its row.
    /// </summary>
    /// <remarks>
    /// Two principals may do this and no third (Decision #27): the item's uploader, and an
    /// administrator. Owning the activity the item sits on is not enough.
    /// </remarks>
    /// <param name="mediaId">The item's id.</param>
    /// <returns>No content; not-found for an id that names nothing; forbidden for a caller who can
    /// read the item but may not remove it.</returns>
    [HttpDelete("/api/media/{mediaId}")]
    public async Task<IActionResult> DeleteMedia(string mediaId)
    {
        MediaOutcome outcome = await _mediaService.DeleteAsync(mediaId);

        return this.ToActionResult(outcome);
    }

    /// <summary>
    /// Maps a service outcome onto HTTP.
    /// </summary>
    /// <remarks>
    /// <c>ValidationProblem</c> rather than a bare <c>BadRequest</c>, so the field-keyed errors
    /// arrive in the shape <c>[ApiController]</c> produces for a body that did not bind at all.
    /// </remarks>
    private IActionResult Respond(ActivityOutcome outcome) => outcome.Kind switch
    {
        ActivityOutcomeKind.Completed => Ok(outcome.Activity),
        ActivityOutcomeKind.Deleted => NoContent(),
        ActivityOutcomeKind.NotFound => NotFound(),
        ActivityOutcomeKind.Forbidden => Forbid(),
        ActivityOutcomeKind.NoCaller => Unauthorized(),
        _ => ValidationProblem(
            new ValidationProblemDetails(new Dictionary<string, string[]>(outcome.Errors!))),
    };
}
