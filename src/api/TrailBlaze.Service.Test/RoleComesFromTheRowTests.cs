namespace TrailBlaze.Service.Test;

using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Model.Profile;

/// <summary>
/// The two ways a role could arrive from anywhere but the <c>users</c> row, asserted to be
/// absent.
/// </summary>
/// <remarks>
/// <para>
/// The rule is a negative — "the row is what counts, and the token is never consulted" — and a
/// negative cannot be driven by a passing behaviour. It is asserted here as a property of two
/// types instead, which is the one shape in which it can fail: add <c>Role</c> to the caller
/// abstraction and a claim becomes a role source that every later authorization check may
/// quite reasonably read; add it to the edit request and a payload becomes one. Both changes
/// compile, both look helpful at the call site, and neither would break any behavioural test —
/// which is exactly why the guard is here rather than left to review.
/// </para>
/// <para>
/// <b>What this is not.</b> It is not evidence that the role is read correctly, that seeding
/// writes it, or that a signed-in user's row says what it should. Those are behaviours and they
/// are asserted against an engine in <see cref="AdminSeedingTests"/> and
/// <see cref="CallerRoleTests"/>. This file answers one question — where the value may come
/// from — and answers it structurally.
/// </para>
/// </remarks>
public sealed class RoleComesFromTheRowTests
{
    /// <summary>
    /// The caller abstraction exposes no role, so there is no claim-shaped source for one.
    /// </summary>
    /// <remarks>
    /// It is synchronous, claims-only, and implemented in the entrance where nothing performs a
    /// data operation; a role on it would have to be read from a token, because there is
    /// nowhere else for it to come from. The role lives on <c>ICallerRoleService</c> instead,
    /// which is a service and therefore allowed to ask the database.
    /// </remarks>
    [Fact]
    public void The_caller_abstraction_offers_no_role()
    {
        Assert.DoesNotContain(
            typeof(IUserContextService).GetProperties(),
            property => string.Equals(property.Name, "Role", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The profile edit request carries no role, so no payload can promote the caller it
    /// belongs to.
    /// </summary>
    /// <remarks>
    /// <c>UpdateProfileAsync</c> assigns four fields and reads none of the others, which is the
    /// behaviour; this is the reason that behaviour cannot quietly stop being true. A
    /// <c>Role</c> property on the request would be bound from the body by the model binder
    /// without a line of code anywhere suggesting it, and the profile route would become a
    /// role-granting endpoint.
    /// </remarks>
    [Fact]
    public void The_profile_edit_request_offers_no_role()
    {
        Assert.DoesNotContain(
            typeof(UpdateProfileRequest).GetProperties(),
            property => string.Equals(property.Name, "Role", StringComparison.OrdinalIgnoreCase));
    }
}
