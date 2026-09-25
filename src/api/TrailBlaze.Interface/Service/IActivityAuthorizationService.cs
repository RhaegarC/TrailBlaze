namespace TrailBlaze.Interface.Service;

using System.Linq.Expressions;
using TrailBlaze.Model.Authorization;
using TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// The whole of the permission rule: who is acting, who may read an entry, who may change one, and
/// who may remove an item of media from it.
/// </summary>
/// <remarks>
/// One service rather than a check beside each route, so the rule is stated once and a change to it
/// changes every route at once. Nothing else may compare a caller with a <c>CreatedBy</c>, or an
/// activity's type against a visibility, for exactly that reason.
/// </remarks>
public interface IActivityAuthorizationService
{
    /// <summary>
    /// The caller the request in flight belongs to: the token's object id, and the role read from
    /// that user's row.
    /// </summary>
    /// <returns>Anonymous for a request naming nobody, answered without a query.</returns>
    Task<Caller> ResolveAsync();

    /// <summary>
    /// The read rule as a predicate, for a query that must filter <em>before</em> it pages: a row the
    /// caller may not see must not consume a page slot, nor be counted in the reported total.
    /// </summary>
    /// <param name="caller">Who is asking.</param>
    /// <returns>Public entries for an anonymous caller; those plus the shared ones and the caller's
    /// own otherwise; everything for an administrator.</returns>
    Expression<Func<Activity, bool>> VisibleTo(Caller caller);

    /// <summary>
    /// The same rule for a row already in hand, which is the shape every single-entry route needs.
    /// </summary>
    /// <param name="activity">The loaded row, or null for an id that names nothing.</param>
    /// <param name="caller">Who is asking.</param>
    /// <returns>True when this caller may read this entry.</returns>
    bool CanRead(Activity? activity, Caller caller);

    /// <summary>
    /// Who may change or delete an entry: its owner, and an administrator.
    /// </summary>
    /// <param name="activity">The loaded row, or null.</param>
    /// <param name="caller">Who is asking.</param>
    /// <returns>True when this caller owns the entry or may override ownership.</returns>
    bool CanMutate(Activity? activity, Caller caller);

    /// <summary>
    /// Who may remove one item of media: the caller who uploaded it, and an administrator
    /// (Decision #27). Owning the entry it sits on is not enough.
    /// </summary>
    /// <param name="media">The item.</param>
    /// <param name="caller">Who is asking.</param>
    /// <returns>True when this caller is the uploader or an administrator.</returns>
    bool CanRemoveMedia(Media media, Caller caller);
}
