namespace TrailBlaze.Model.Media;

/// <summary>
/// Which of the two answers minting a read URL produced.
/// </summary>
public enum MediaUrlOutcomeKind
{
    /// <summary>A URL was signed, and carries the expiry reported beside it.</summary>
    Minted,

    /// <summary>No item with that id is visible to this caller.</summary>
    NotFound,
}

/// <summary>
/// The result of asking for an item's read URL, and the reason for it.
/// </summary>
/// <remarks>
/// A result rather than an exception, as the other media outcomes are. Two answers and not four: a
/// caller who may see an item may fetch it, so there is no forbidden case to keep separate, and an
/// item this caller may not see is answered not-found exactly as an absent one is. There is no
/// no-caller case either — the mint is a read, so a token is not what it asks for.
/// </remarks>
public sealed class MediaUrlOutcome
{
    private MediaUrlOutcome(MediaUrlOutcomeKind kind, MediaUrlResponse? url)
    {
        Kind = kind;
        Url = url;
    }

    /// <summary>Which of the two answers this is.</summary>
    public MediaUrlOutcomeKind Kind { get; }

    /// <summary>The signed URL and its expiry. Set only when <see cref="Kind"/> is <c>Minted</c>.</summary>
    public MediaUrlResponse? Url { get; }

    /// <summary>A URL was signed.</summary>
    public static MediaUrlOutcome Minted(MediaUrlResponse url) =>
        new(MediaUrlOutcomeKind.Minted, url);

    /// <summary>No item with that id is visible to this caller.</summary>
    public static MediaUrlOutcome NotFound() => new(MediaUrlOutcomeKind.NotFound, null);
}
