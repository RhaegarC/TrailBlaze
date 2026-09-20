namespace TrailBlaze.Model.Media;

/// <summary>
/// Which of the seven answers a media write produced.
/// </summary>
public enum MediaOutcomeKind
{
    /// <summary>An item was stored.</summary>
    Uploaded,

    /// <summary>An item was deleted. There is no body.</summary>
    Deleted,

    /// <summary>The upload was refused, and nothing was written.</summary>
    Rejected,

    /// <summary>The activity already holds the maximum number of items.</summary>
    LimitReached,

    /// <summary>No activity or item with that id is visible to this caller, including a deleted one.</summary>
    NotFound,

    /// <summary>The request names no caller.</summary>
    NoCaller,

    /// <summary>The caller may see the item but may not remove it.</summary>
    Forbidden,
}

/// <summary>
/// The result of a media write, and the reason for it.
/// </summary>
/// <remarks>
/// A result rather than exceptions, for the same reason as <c>ActivityOutcome</c>: a refused
/// upload is an ordinary answer. Only <see cref="Item"/> and <see cref="Errors"/> are ever set,
/// never both.
/// </remarks>
public sealed class MediaOutcome
{
    private MediaOutcome(
        MediaOutcomeKind kind,
        MediaResponse? item,
        IReadOnlyDictionary<string, string[]>? errors)
    {
        Kind = kind;
        Item = item;
        Errors = errors;
    }

    /// <summary>Which of the seven answers this is.</summary>
    public MediaOutcomeKind Kind { get; }

    /// <summary>The item as it now stands, set only when <see cref="Kind"/> is <c>Uploaded</c>.</summary>
    public MediaResponse? Item { get; }

    /// <summary>Why the upload was refused, keyed by field. Set only when <see cref="Kind"/> is
    /// <c>Rejected</c>.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    /// <summary>An item was stored.</summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public static MediaOutcome Uploaded(MediaResponse item) =>
        new(MediaOutcomeKind.Uploaded, item, null);

    /// <summary>An item was deleted.</summary>
    /// <returns></returns>
    public static MediaOutcome Deleted() => new(MediaOutcomeKind.Deleted, null, null);

    /// <summary>The upload was refused, and nothing was written.</summary>
    /// <param name="errors"></param>
    /// <returns></returns>
    public static MediaOutcome Rejected(IReadOnlyDictionary<string, string[]> errors) =>
        new(MediaOutcomeKind.Rejected, null, errors);

    /// <summary>The activity is at its item limit.</summary>
    /// <returns></returns>
    public static MediaOutcome LimitReached() => new(MediaOutcomeKind.LimitReached, null, null);

    /// <summary>No activity or item with that id is visible.</summary>
    /// <returns></returns>
    public static MediaOutcome NotFound() => new(MediaOutcomeKind.NotFound, null, null);

    /// <summary>The request names no caller.</summary>
    /// <returns></returns>
    public static MediaOutcome NoCaller() => new(MediaOutcomeKind.NoCaller, null, null);

    /// <summary>The caller may see the item but may not remove it.</summary>
    /// <returns></returns>
    public static MediaOutcome Forbidden() => new(MediaOutcomeKind.Forbidden, null, null);
}
