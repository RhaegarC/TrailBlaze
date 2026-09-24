namespace TrailBlaze.Model.Activity;

/// <summary>
/// Which of the six answers a service call produced.
/// </summary>
public enum ActivityOutcomeKind
{
    /// <summary>
    /// An activity was read, created or updated.
    /// </summary>
    Completed,

    /// <summary>
    /// An activity was deleted. There is no body.
    /// </summary>
    Deleted,

    /// <summary>
    /// The input was refused, and nothing was written.
    /// </summary>
    Rejected,

    /// <summary>
    /// No activity with that id is visible, including one that was deleted.
    /// </summary>
    NotFound,

    /// <summary>
    /// The request names no caller, so an entry could not be attributed.
    /// </summary>
    NoCaller,

    /// <summary>
    /// The caller may read the entry but may not change it. Distinct from
    /// <see cref="NotFound"/>, which withholds the entry's existence instead: one is a refusal the
    /// caller is told about, the other is a fact they are not.
    /// </summary>
    Forbidden,
}

/// <summary>
/// The result of an activity operation, and the reason for it.
/// </summary>
/// <remarks>
/// A result rather than exceptions: a blank title is an ordinary answer, not a fault. Only
/// <see cref="Activity"/> and <see cref="Errors"/> are ever set, never both.
/// </remarks>
public sealed class ActivityOutcome
{
    private ActivityOutcome(
        ActivityOutcomeKind kind,
        ActivityResponse? activity,
        IReadOnlyDictionary<string, string[]>? errors)
    {
        Kind = kind;
        Activity = activity;
        Errors = errors;
    }

    /// <summary>
    /// Which of the six answers this is.
    /// </summary>
    public ActivityOutcomeKind Kind { get; }

    /// <summary>
    /// The activity as it now stands, set only when <see cref="Kind"/> is <c>Completed</c>.
    /// </summary>
    public ActivityResponse? Activity { get; }

    /// <summary>
    /// Why the input was refused, keyed by field as <c>ValidationProblemDetails.Errors</c> is.
    /// Set only when <see cref="Kind"/> is <c>Rejected</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    /// <summary>
    /// An activity was read, created or updated.
    /// </summary>
    /// <param name="activity"></param>
    /// <returns></returns>
    public static ActivityOutcome Completed(ActivityResponse activity) =>
        new(ActivityOutcomeKind.Completed, activity, null);

    /// <summary>
    /// An activity was deleted.
    /// </summary>
    /// <returns></returns>
    public static ActivityOutcome Deleted() => new(ActivityOutcomeKind.Deleted, null, null);

    /// <summary>
    /// The input was refused, and nothing was written.
    /// </summary>
    /// <param name="errors"></param>
    /// <returns></returns>
    public static ActivityOutcome Rejected(IReadOnlyDictionary<string, string[]> errors) =>
        new(ActivityOutcomeKind.Rejected, null, errors);

    /// <summary>
    /// No activity with that id is visible.
    /// </summary>
    /// <returns></returns>
    public static ActivityOutcome NotFound() => new(ActivityOutcomeKind.NotFound, null, null);

    /// <summary>
    /// The request names no caller, so an entry could not be attributed.
    /// </summary>
    /// <returns></returns>
    public static ActivityOutcome NoCaller() => new(ActivityOutcomeKind.NoCaller, null, null);

    /// <summary>
    /// The caller may read the entry but may not change it.
    /// </summary>
    /// <returns></returns>
    public static ActivityOutcome Forbidden() => new(ActivityOutcomeKind.Forbidden, null, null);
}
