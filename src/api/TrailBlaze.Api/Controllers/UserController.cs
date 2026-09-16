namespace TrailBlaze.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.Profile;

/// <summary>
/// The caller's own record and profile.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>[Authorize]</c> is on the type, not on each action.</b> The rule this controller
/// keeps is that no route on it is reachable anonymously, and an attribute per action is
/// exactly the kind of rule that the next route gets added without. Declaring it once makes
/// forgetting it impossible rather than merely unlikely.
/// </para>
/// <para>
/// <b>No route takes a user id.</b> That is the whole of the "self-service only" guarantee at
/// this layer: there is no parameter through which a caller could name someone else, so the
/// ownership question cannot be answered wrongly here because it cannot be asked. What the
/// routes act on is the request in flight, resolved in the service from the validated token.
/// </para>
/// </remarks>
[ApiController]
[Authorize]
[Route("[controller]")]
public class UserController(IUserService userService) : Controller
{
    private readonly IUserService _userService = userService;

    [HttpGet("index")]
    public IActionResult Index()
    {
        return Ok("Welcome");
    }

    /// <summary>
    /// The caller's profile, provisioned on their first call.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        UserProfileResponse? profile = await _userService.GetProfileAsync();

        // Authenticated, but the token carries no object id to identify the caller by.
        // That is not a missing token, so there is no challenge to issue and no profile
        // to return.
        return profile is null ? Unauthorized() : Ok(profile);
    }

    /// <summary>
    /// Edits the caller's own row — display name, bio, and the two presentation preferences.
    /// </summary>
    /// <remarks>
    /// <c>Role</c>, <c>Email</c> and <c>Id</c> cannot be changed by this route whatever the
    /// body carries: <see cref="UpdateProfileRequest"/> has no such properties, so there is
    /// nothing for a body to bind to. The privilege-escalation guard is that absence rather
    /// than a check that could be removed.
    /// </remarks>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        ProfileOutcome outcome = await _userService.UpdateProfileAsync(request);

        return Respond(outcome);
    }

    /// <summary>
    /// Uploads the caller's avatar, replacing any previous one, and answers with the profile
    /// carrying the new public URL.
    /// </summary>
    /// <remarks>
    /// The 10 MB cap is checked against a buffered body rather than a streamed one, so the
    /// bytes are held in memory before they can be rejected. That is acceptable while the
    /// largest upload is 10 MB and Kestrel's default body limit is 30 MB, which is the outer
    /// guard; feature 06's 200 MB videos are what will force both numbers to be raised
    /// deliberately, and that is where the streaming question belongs rather than here.
    /// </remarks>
    /// <param name="file">The image to store. Validated against the shared image allowlist and
    /// size cap before anything is written.</param>
    [HttpPost("me/avatar")]
    public async Task<IActionResult> SetAvatar([FromForm] IFormFile? file)
    {
        if (file is null)
        {
            // Without this the route would dereference a null and 500. A body with no file
            // part is a malformed request, which is a 400.
            return BadRequest(Constant.Message.NoFileUploaded);
        }

        await using Stream content = file.OpenReadStream();

        ProfileOutcome outcome = await _userService.SetAvatarAsync(
            content, file.ContentType, file.Length);

        return Respond(outcome);
    }

    /// <summary>
    /// Removes the caller's avatar, clearing the field and deleting the blob. A caller who has
    /// no avatar gets a success rather than a 404 — they asked for it not to be there.
    /// </summary>
    [HttpDelete("me/avatar")]
    public async Task<IActionResult> RemoveAvatar()
    {
        ProfileOutcome outcome = await _userService.RemoveAvatarAsync();

        return Respond(outcome);
    }

    /// <summary>
    /// Maps a service outcome onto HTTP, which is all this layer adds to it: the service
    /// reports what happened and knows nothing about status codes.
    /// </summary>
    /// <remarks>
    /// <c>ValidationProblem</c> rather than a bare <c>BadRequest</c> so the field-keyed errors
    /// arrive as <c>ValidationProblemDetails</c> — the same shape <c>[ApiController]</c>
    /// produces for a malformed body, so a client has one error format to handle rather than
    /// two that depend on where the check happened to live.
    /// </remarks>
    private IActionResult Respond(ProfileOutcome outcome)
    {
        if (!outcome.HasCaller)
        {
            return Unauthorized();
        }

        if (outcome.Succeeded)
        {
            return Ok(outcome.Profile);
        }

        return ValidationProblem(
            new ValidationProblemDetails(new Dictionary<string, string[]>(outcome.Errors!)));
    }
}
