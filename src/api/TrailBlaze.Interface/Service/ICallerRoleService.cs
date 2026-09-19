namespace TrailBlaze.Interface.Service;

/// <summary>
/// The caller's role, as the <c>users</c> row holds it — the one place a later authorization
/// check asks.
/// </summary>
/// <remarks>
/// <para>
/// This is the identity abstraction's answer to "what may this caller do", and it is a service
/// rather than a member of <c>IUserContextService</c> for a reason that is not taste. That
/// abstraction reports what the request in flight carries: it is synchronous, it reads claims,
/// and it is implemented in the entrance, where nothing performs a data operation. The role is
/// not in the request — it is in the database, keyed by the object id the request carries — so
/// putting it there would have the entrance query the database and would make its whole
/// contract asynchronous. The role gets its own type instead, and the rule that matters is
/// unchanged: this value comes from the row, and the token is never asked.
/// </para>
/// <para>
/// Nothing here provisions. A caller with no row has no role rather than acquiring one, which
/// is what lets an authorization check fail closed for somebody the app has not otherwise seen.
/// </para>
/// </remarks>
public interface ICallerRoleService
{
    /// <summary>
    /// The role on the caller's own row.
    /// </summary>
    /// <remarks>
    /// No parameter, for the reason every member of <c>IUserService</c> takes none: the caller
    /// is the request in flight, so there is no argument through which one caller could be
    /// asked about another.
    /// </remarks>
    /// <returns>
    /// The stored role, or <c>null</c> when the request carries no Entra object id, no row
    /// exists for the one it carries, or that row has been soft-deleted. Null is the answer to
    /// all three on purpose: they are the same fact — this app does not know of an
    /// authenticated person here — and a caller that had to tell them apart would be a caller
    /// deciding what each missing case grants.
    /// </returns>
    Task<string?> GetRoleAsync();
}
