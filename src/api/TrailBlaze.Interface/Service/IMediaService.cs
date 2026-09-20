namespace TrailBlaze.Interface.Service;

using TrailBlaze.Model.Media;

/// <summary>
/// The media an activity carries: what may be stored, who may contribute it, and who may remove it.
/// </summary>
/// <remarks>
/// Media is collaborative (Decision #27), so the gate on both writing and reading here is whether the
/// caller can <em>read the activity</em> — not whether they wrote it.
/// </remarks>
public interface IMediaService
{
    /// <summary>
    /// Stores an uploaded image or video against an activity, attributed to the caller.
    /// </summary>
    /// <remarks>
    /// A refusal leaves no blob and no row, so a caller never reasons about partial state.
    /// </remarks>
    /// <param name="activityId">The activity the item is attached to.</param>
    /// <param name="content">The uploaded bytes.</param>
    /// <param name="contentType">The content type the client declared.</param>
    /// <param name="sizeBytes">The number of bytes the upload carries.</param>
    /// <param name="fileName">The client's file name. Kept for display, never for addressing.</param>
    /// <returns>The stored item, or the reason it was refused.</returns>
    Task<MediaOutcome> UploadAsync(
        string activityId,
        Stream content,
        string? contentType,
        long sizeBytes,
        string? fileName);

    /// <summary>
    /// The metadata of every item an activity carries, each naming its uploader.
    /// </summary>
    /// <param name="activityId">The activity to list.</param>
    /// <returns>The items, or not-found for an activity this caller may not read.</returns>
    Task<MediaListing> ListAsync(string activityId);

    /// <summary>
    /// Removes an item: its blob and its row.
    /// </summary>
    /// <param name="mediaId">The item's id.</param>
    /// <returns>Deleted; not-found for an id that names nothing; or forbidden for a caller who can
    /// read the item but is not permitted to remove it.</returns>
    Task<MediaOutcome> DeleteAsync(string mediaId);
}
