namespace TrailBlaze.Service;

using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// Answers what the caller may do by looking up the row their object id names.
/// </summary>
/// <remarks>
/// <para>
/// <b>The token is never asked.</b> The only value read from the request is the Entra object
/// id, which is the <c>users</c> key and nothing more; the role is then whatever that row says.
/// A token asserting anything about roles changes no answer here, and there is no branch in
/// which a claim could, because <c>IUserContextService</c> exposes no role to read —
/// <c>RoleComesFromTheRowTests</c> asserts that absence upstream.
/// </para>
/// <para>
/// <b>A read that does not write.</b> The row is looked up, not provisioned, which is the one
/// place this deliberately differs from <c>IUserService.GetOrCreateAsync</c>. A profile request
/// is a request to have a row and creates one; an authorization check is a question, and a
/// question that provisions would turn every rejected request into a user-creating operation
/// and every caller into somebody the app knows. A caller with no row simply has no role, which
/// is the answer that grants nothing.
/// </para>
/// <para>
/// <b>Deleted rows are already excluded.</b> The soft-delete filter is applied by convention to
/// every entity here, so a row the app has retired is invisible to this query without this type
/// asking for that — a deleted administrator confers nothing, and the reason is one shared rule
/// rather than a check written here that a later edit could drop.
/// </para>
/// </remarks>
public sealed class CallerRoleService(IDbRepository dbRepository, IUserContextService userContext)
    : ICallerRoleService
{
    /// <inheritdoc/>
    public async Task<string?> GetRoleAsync()
    {
        string? entraObjectId = userContext.EntraObjectId;

        // No object id is not an error and not a role. A request that carries no identity is
        // the anonymous one, and there is nothing to key a lookup on and nothing to assume.
        if (string.IsNullOrWhiteSpace(entraObjectId))
        {
            return null;
        }

        User? user = await dbRepository.GetAsync<User>(existing => existing.Id == entraObjectId);

        return user?.Role;
    }
}
