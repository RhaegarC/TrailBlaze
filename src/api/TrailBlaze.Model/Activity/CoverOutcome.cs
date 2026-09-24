namespace TrailBlaze.Model.Activity;

/// <summary>
/// Which of the five answers a cover write produced.
/// </summary>
public enum CoverOutcomeKind
{
    /// <summary>A cover was stored, and the activity now points at it.</summary>
    Uploaded,

    /// <summary>The upload was refused, and nothing was written.</summary>
    Rejected,

    /// <summary>No activity with that id is visible to this caller, including a deleted one.</summary>
    NotFound,

    /// <summary>The request names no caller.</summary>
    NoCaller,

    /// <summary>
    /// The caller may read the activity but may not give it a face. Distinct from
    /// <see cref="NotFound"/>: a cover is the entry's own field, so unlike media it is the owner's
    /// to change and a stranger's read access is not a licence to replace it.
    /// </summary>
    Forbidden,
}

/// <summary>
/// The result of a cover upload, and the reason for it.
/// </summary>
/// <remarks>
/// A result rather than exceptions, for the same reason as <c>ActivityOutcome</c>: a refused image
/// is an ordinary answer. Only <see cref="Cover"/> and <see cref="Errors"/> are ever set, never
/// both.
/// </remarks>
public sealed class CoverOutcome
{
    private CoverOutcome(
        CoverOutcomeKind kind,
        CoverResponse? cover,
        IReadOnlyDictionary<string, string[]>? errors)
    {
        Kind = kind;
        Cover = cover;
        Errors = errors;
    }

    /// <summary>Which of the five answers this is.</summary>
    public CoverOutcomeKind Kind { get; }

    /// <summary>The cover as it now stands, set only when <see cref="Kind"/> is
    /// <c>Uploaded</c>.</summary>
    public CoverResponse? Cover { get; }

    /// <summary>Why the upload was refused, keyed by field. Set only when <see cref="Kind"/> is
    /// <c>Rejected</c>.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    /// <summary>A cover was stored.</summary>
    /// <param name="cover"></param>
    /// <returns></returns>
    public static CoverOutcome Uploaded(CoverResponse cover) =>
        new(CoverOutcomeKind.Uploaded, cover, null);

    /// <summary>The upload was refused, and nothing was written.</summary>
    /// <param name="errors"></param>
    /// <returns></returns>
    public static CoverOutcome Rejected(IReadOnlyDictionary<string, string[]> errors) =>
        new(CoverOutcomeKind.Rejected, null, errors);

    /// <summary>No activity with that id is visible.</summary>
    /// <returns></returns>
    public static CoverOutcome NotFound() => new(CoverOutcomeKind.NotFound, null, null);

    /// <summary>The request names no caller.</summary>
    /// <returns></returns>
    public static CoverOutcome NoCaller() => new(CoverOutcomeKind.NoCaller, null, null);

    /// <summary>The caller may read the activity but may not change its cover.</summary>
    /// <returns></returns>
    public static CoverOutcome Forbidden() => new(CoverOutcomeKind.Forbidden, null, null);
}
