namespace TrailBlaze.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model.Media;

/// <summary>
/// One stored item, addressed by its own id rather than through the activity it belongs to.
/// </summary>
/// <remarks>
/// <c>[Authorize]</c> on the type, as on the other controllers: media requires sign-in whatever its
/// activity's visibility (Decision #2), so no route here is ever anonymous.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MediaController(IMediaService mediaService) : ControllerBase
{
    private readonly IMediaService _mediaService = mediaService;

    /// <summary>
    /// Removes an item: its blob and its row.
    /// </summary>
    /// <remarks>
    /// Three principals may do this and no fourth (Decision #27): the uploader, the owner of the
    /// activity the item sits on, and an administrator. The administrator is not among them yet —
    /// nothing can read a role, see <c>IActivityAccessService</c> — so one is judged as an ordinary
    /// user today.
    /// </remarks>
    /// <param name="id">The item's id.</param>
    /// <returns>No content; not-found for an id that names nothing; forbidden for a caller who can
    /// read the item but may not remove it.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        MediaOutcome outcome = await _mediaService.DeleteAsync(id);

        return this.ToActionResult(outcome);
    }
}
