namespace TrailBlaze.Service.Test;

using TrailBlaze.Interface.Service;

/// <summary>
/// The visibility rule has one home, asserted as an absence.
/// </summary>
/// <remarks>
/// A rule with two homes is two rules, and the copy that drifts is the one nobody edits. This is a
/// negative, so it cannot be driven by a passing behaviour: it is asserted as a property of the
/// contract instead, which is the one shape in which it can fail. The activity service used to carry
/// the read half, and a route that could still reach it there would be reaching a second answer.
/// </remarks>
public sealed class OneRuleOneHomeTests
{
    [Fact]
    public void The_activity_service_offers_no_visibility_rule()
    {
        string[] members = [.. typeof(IActivityService).GetMethods().Select(method => method.Name)];

        Assert.DoesNotContain(nameof(IActivityAuthorizationService.VisibleTo), members);
        Assert.DoesNotContain(nameof(IActivityAuthorizationService.CanRead), members);
        Assert.DoesNotContain(nameof(IActivityAuthorizationService.CanMutate), members);
    }

    /// <summary>
    /// Resolving who the caller is belongs with the rule that reads the role, not on the ambient
    /// caller abstraction: a contract that reports the request must not be the thing that decides
    /// what the request may do.
    /// </summary>
    [Fact]
    public void The_caller_abstraction_resolves_nothing()
    {
        string[] members = [
            .. typeof(TrailBlaze.Interface.Infrastructure.IUserContextService)
                .GetMethods()
                .Select(method => method.Name)];

        Assert.DoesNotContain("ResolveAsync", members);
        Assert.DoesNotContain("IsAdmin", members);
    }
}
