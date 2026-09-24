namespace TrailBlaze.Interface.Service;

using TrailBlaze.Model.Activity;

/// <summary>
/// The rules an activity must satisfy and the operations that store one.
/// </summary>
/// <remarks>
/// It carries no permission rule of its own: who may read an entry, who may change one and who may
/// remove an item of media are all decided by <see cref="IActivityAuthorizationService"/>, and this
/// service consults it. A second copy here would be a second rule.
/// </remarks>
public interface IActivityService
{
    /// <summary>
    /// Checks the fields a caller supplied.
    /// </summary>
    /// <param name="input">The values from either the create or the update body.</param>
    /// <returns>Reasons keyed by field, empty when the input is valid.</returns>
    IReadOnlyDictionary<string, string[]> Validate(IActivityInput input);

    /// <summary>
    /// Stores a new activity, attributed to the caller.
    /// </summary>
    /// <param name="request">The fields to store.</param>
    /// <returns>The stored activity, the reasons the input was refused, or no caller.</returns>
    Task<ActivityOutcome> CreateAsync(CreateActivityRequest request);

    /// <summary>
    /// Reads one activity.
    /// </summary>
    /// <param name="id">The activity's id.</param>
    /// <returns>The activity, or not-found. A deleted activity is not found.</returns>
    Task<ActivityOutcome> GetAsync(string id);

    /// <summary>
    /// One page of the activities the caller may read, newest first.
    /// </summary>
    /// <param name="page">Zero-based page index. A negative value is read as the first page.</param>
    /// <param name="pageSize">Rows per page, clamped to the accepted range.</param>
    /// <returns>The page, and the size of everything the caller may read.</returns>
    Task<ActivityPage> GetPageAsync(int page, int pageSize);

    /// <summary>
    /// Replaces the editable fields of an activity, including its <c>Type</c>.
    /// </summary>
    /// <param name="id">The activity's id.</param>
    /// <param name="request">The fields to store.</param>
    /// <returns>The updated activity, the reasons the input was refused, or not-found.</returns>
    Task<ActivityOutcome> UpdateAsync(string id, UpdateActivityRequest request);

    /// <summary>
    /// Stores an image as an activity's cover, replacing any it already has, and points the
    /// activity at the new one.
    /// </summary>
    /// <remarks>
    /// The destination is decided here rather than by the caller: the container follows the
    /// activity's <c>Type</c>, and the container is the whole of the public/private answer
    /// (Decision #29).
    /// </remarks>
    /// <param name="activityId">The activity's id.</param>
    /// <param name="content">The image bytes.</param>
    /// <param name="contentType">The declared content type, as sent by the client.</param>
    /// <param name="sizeBytes">The number of bytes the upload carries.</param>
    /// <returns>The cover's URL, the reasons the upload was refused, or not-found.</returns>
    Task<CoverOutcome> UploadCoverAsync(
        string activityId,
        Stream content,
        string? contentType,
        long sizeBytes);

    /// <summary>
    /// Soft-deletes an activity: the row is retained and hidden from every read.
    /// </summary>
    /// <param name="id">The activity's id.</param>
    /// <returns>Deleted, or not-found for an id that is unknown or already deleted.</returns>
    Task<ActivityOutcome> DeleteAsync(string id);
}
