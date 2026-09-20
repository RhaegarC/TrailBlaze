namespace TrailBlaze.Service;

using System.Linq.Expressions;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;

/// <inheritdoc/>
public sealed class ActivityAccessService : IActivityAccessService
{
    /// <inheritdoc/>
    public Expression<Func<Activity, bool>> VisibleTo(string? caller) =>
        string.IsNullOrWhiteSpace(caller)
            ? activity => activity.Type == Constant.ActivityType.Public
            : activity => activity.Type == Constant.ActivityType.Public
                || activity.Type == Constant.ActivityType.Shared
                || activity.CreatedByUserId == caller;

    /// <inheritdoc/>
    public bool CanRead(Activity activity, string? caller)
    {
        ArgumentNullException.ThrowIfNull(activity);

        // Compiled from the expression the queries use rather than restated in C#. A second copy
        // of an access rule is a second rule, and the two part company the first time either is
        // edited — which for this rule means a row one route hides and another serves.
        return VisibleTo(caller).Compile()(activity);
    }
}
