namespace TrailBlaze.Model.Activity;

/// <summary>Which of the five answers a service call produced.</summary>
public enum ActivityOutcomeKind
{
    /// <summary>An activity was read, created or updated.</summary>
    Completed,

    /// <summary>An activity was deleted. There is no body.</summary>
    Deleted,

    /// <summary>The input was refused, and nothing was written.</summary>
    Rejected,

    /// <summary>No activity with that id is visible — including one that was deleted.</summary>
    NotFound,

    /// <summary>The request names no caller, so an entry could not be attributed.</summary>
    NoCaller,
}

/// <summary>The result of an activity operation, and the reason for it.</summary>
/// <remarks>
/// A result rather than exceptions: a blank title is an ordinary answer, not a fault, and
/// throwing would push the mapping to HTTP up into a layer that knows nothing about status
/// codes. Only <see cref="Activity"/> and <see cref="Errors"/> are ever set, never both.
/// </remarks>
public sealed class ActivityOutcome
{
    private ActivityOutcome()
    {
    }

    public ActivityOutcomeKind Kind { get; private init; }

    /// <summary>The activity as it now stands, re-read after the write rather than echoed from
    /// the request. Set only when <see cref="Kind"/> is <c>Completed</c>.</summary>
    public ActivityResponse? Activity { get; private init; }

    /// <summary>Why the input was refused, keyed by field as
    /// <c>ValidationProblemDetails.Errors</c> is. Set only when <see cref="Kind"/> is
    /// <c>Rejected</c>.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; private init; }

    public static ActivityOutcome Completed(ActivityResponse activity) =>
        new() { Kind = ActivityOutcomeKind.Completed, Activity = activity };

    public static ActivityOutcome Deleted() => new() { Kind = ActivityOutcomeKind.Deleted };

    public static ActivityOutcome Rejected(IReadOnlyDictionary<string, string[]> errors) =>
        new() { Kind = ActivityOutcomeKind.Rejected, Errors = errors };

    public static ActivityOutcome NotFound() => new() { Kind = ActivityOutcomeKind.NotFound };

    public static ActivityOutcome NoCaller() => new() { Kind = ActivityOutcomeKind.NoCaller };
}
