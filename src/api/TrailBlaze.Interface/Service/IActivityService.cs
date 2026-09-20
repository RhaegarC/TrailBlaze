namespace TrailBlaze.Interface.Service;

using System.Linq.Expressions;
using TrailBlaze.Model.Activity;
using TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// The rules an activity must satisfy, the operations that store one, and who may read it.
/// </summary>
/// <remarks>
/// The read half of the permission rule lives here rather than on a contract of its own, so the
/// visibility filter and the reads it filters are read together. The mutation half — who may edit or
/// delete, and the administrator's override of both — is feature 09's.
/// </remarks>
public interface IActivityService
{
    /// <summary>
    /// The read rule as a predicate, for a query that must filter <em>before</em> it pages: a row
    /// the caller may not see must not consume a page slot, nor be counted in the reported total.
    /// </summary>
    /// <param name="caller">The caller's object id, or null for an anonymous request.</param>
    /// <returns>Public entries for a token-less caller; those plus the shared ones and the
    /// caller's own private ones otherwise.</returns>
    Expression<Func<Activity, bool>> VisibleTo(string? caller);

    /// <summary>
    /// The same rule for a row already in hand, which is the shape the single-row reads need.
    /// </summary>
    /// <param name="activity">The loaded row.</param>
    /// <param name="caller">The caller's object id, or null for an anonymous request.</param>
    /// <returns>True when this caller may read this entry.</returns>
    bool CanRead(Activity activity, string? caller);

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
    /// Soft-deletes an activity: the row is retained and hidden from every read.
    /// </summary>
    /// <param name="id">The activity's id.</param>
    /// <returns>Deleted, or not-found for an id that is unknown or already deleted.</returns>
    Task<ActivityOutcome> DeleteAsync(string id);
}
