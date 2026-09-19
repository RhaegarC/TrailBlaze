namespace TrailBlaze.Service.Test;

using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Model.Profile;

/// <summary>
/// The two ways a role could arrive from anywhere but the <c>users</c> row, asserted to be
/// absent.
/// </summary>
/// <remarks>
/// The rule is a negative, and a negative cannot be driven by a passing behaviour. So it is
/// asserted as a property of two types instead, which is the one shape in which it can fail: a
/// <c>Role</c> on the caller abstraction makes a claim a role source, and a <c>Role</c> on the
/// edit request makes a payload one. Both compile, both look helpful at the call site, and
/// neither would break a behavioural test.
/// </remarks>
public sealed class RoleComesFromTheRowTests
{
    /// <summary>
    /// The caller abstraction exposes no role, so there is no claim-shaped source for one.
    /// </summary>
    /// <remarks>
    /// It is synchronous, claims-only and implemented in the entrance, where nothing performs a
    /// data operation — so a role on it could only come from the token.
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
    /// A <c>Role</c> property here would be bound from the body by the model binder without a
    /// line of code anywhere suggesting it, and the profile route would become a role-granting
    /// endpoint.
    /// </remarks>
    [Fact]
    public void The_profile_edit_request_offers_no_role()
    {
        Assert.DoesNotContain(
            typeof(UpdateProfileRequest).GetProperties(),
            property => string.Equals(property.Name, "Role", StringComparison.OrdinalIgnoreCase));
    }
}
