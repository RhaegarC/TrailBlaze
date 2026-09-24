namespace TrailBlaze.Service;

using System.Linq.Expressions;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.Authorization;
using TrailBlaze.Model.DatabaseEntity;

/// <inheritdoc/>
public sealed class ActivityAuthorizationService(
    IDbRepository dbRepository,
    IUserContextService userContext) : IActivityAuthorizationService
{
    /// <inheritdoc/>
    public async Task<Caller> ResolveAsync()
    {
        string? callerId = userContext.EntraObjectId;

        // Nothing to look up, and nothing to look it up for: an anonymous request has no row, and
        // reading one would be the only query the public list ever made.
        if (string.IsNullOrWhiteSpace(callerId))
        {
            return Caller.Anonymous;
        }

        User? row = await dbRepository.GetAsync<User>(user => user.Id == callerId);

        // The comparison is against the admin value rather than away from the ordinary one, so a
        // column that grew a third role would grant nothing by accident. A caller with no row is one
        // the application has not provisioned yet, which is an ordinary user rather than a fault.
        return new Caller(callerId, row?.Role == Constant.UserRole.Admin);
    }

    /// <inheritdoc/>
    public Expression<Func<Activity, bool>> VisibleTo(Caller caller) =>
        caller.IsAdmin
            ? activity => true
            : caller.IsSignedIn
                ? activity => activity.Type == Constant.ActivityType.Public
                    || activity.Type == Constant.ActivityType.Shared
                    || activity.CreatedBy == caller.Id
                : activity => activity.Type == Constant.ActivityType.Public;

    /// <inheritdoc/>
    public bool CanRead(Activity? activity, Caller caller) =>
        // Compiled from the expression the queries use rather than restated in C#: a second copy of
        // an access rule is a second rule.
        activity is not null && VisibleTo(caller).Compile()(activity);

    /// <inheritdoc/>
    public bool CanMutate(Activity? activity, Caller caller) =>
        activity is not null
        && (caller.IsAdmin
            || (caller.IsSignedIn && activity.CreatedBy == caller.Id));

    /// <inheritdoc/>
    public bool CanRemoveMedia(Media media, Activity? activity, Caller caller) =>
        caller.IsAdmin
        || (caller.IsSignedIn
            && (media.CreatedBy == caller.Id || activity?.CreatedBy == caller.Id));
}
