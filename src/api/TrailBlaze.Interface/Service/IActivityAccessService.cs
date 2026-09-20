namespace TrailBlaze.Interface.Service;

using System.Linq.Expressions;
using TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// Who may read an activity, in one place, so every route that asks answers the same way.
/// </summary>
/// <remarks>
/// <para>
/// Visibility is one axis of the permission rule and ownership is the other. This contract holds
/// the <b>read</b> half — the half the list, the detail read and the media routes all consult and
/// none may restate, because a second copy of an access rule is a second rule.
/// </para>
/// <para>
/// The mutation half — who may edit or delete, and the administrator's override of both — is
/// feature 09's, which extends this contract rather than adding a rival one beside it.
/// </para>
/// <para>
/// <b>The administrator branch does not exist yet, and cannot.</b> It reads a role, and no layer
/// exposes one: <c>IUserContextService</c> carries none, and a role's only source is the
/// <c>users</c> row — a read this rule deliberately does not perform. Until 09 lands, an
/// administrator reads what a signed-in user reads.
/// </para>
/// </remarks>
public interface IActivityAccessService
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
    /// The same rule for a row already in hand, which is the shape the single-row reads need. It
    /// is compiled from <see cref="VisibleTo"/> rather than written a second time.
    /// </summary>
    /// <param name="activity">The loaded row.</param>
    /// <param name="caller">The caller's object id, or null for an anonymous request.</param>
    /// <returns>True when this caller may read this entry.</returns>
    bool CanRead(Activity activity, string? caller);
}
