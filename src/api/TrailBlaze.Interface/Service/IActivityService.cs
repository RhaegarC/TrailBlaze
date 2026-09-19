namespace TrailBlaze.Interface.Service;

using TrailBlaze.Model.Activity;

/// <summary>
/// Create, read, update and delete one activity.
/// </summary>
/// <remarks>
/// This slice stores <c>Type</c> and does not enforce it: who may read an entry is feature 05's
/// rule and who may mutate one is feature 09's, so every route here answers for any id it is
/// given. That split is deliberate — the CRUD mechanics are tested on their own.
/// </remarks>
public interface IActivityService
{
    /// <summary>
    /// Stores a new activity, attributed to the caller.
    /// </summary>
    /// <param name="request">The fields to store.</param>
    /// <returns>The stored activity, the reasons the input was refused, or no caller.</returns>
    Task<ActivityOutcome> CreateAsync(CreateActivityRequest request);

    /// <summary>Reads one activity.</summary>
    /// <param name="id">The activity's id.</param>
    /// <returns>The activity, or not-found. A deleted activity is not found.</returns>
    Task<ActivityOutcome> GetAsync(string id);

    /// <summary>
    /// Replaces the editable fields of an activity, including its <c>Type</c>.
    /// </summary>
    /// <param name="id">The activity's id.</param>
    /// <param name="request">The fields to store.</param>
    /// <returns>The updated activity, the reasons the input was refused, or not-found.</returns>
    Task<ActivityOutcome> UpdateAsync(string id, UpdateActivityRequest request);

    /// <summary>
    /// Soft-deletes an activity: the row is retained and hidden from every read.
    /// </summary>
    /// <param name="id">The activity's id.</param>
    /// <returns>Deleted, or not-found for an id that is unknown or already deleted.</returns>
    Task<ActivityOutcome> DeleteAsync(string id);
}
