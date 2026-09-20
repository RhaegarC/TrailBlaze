namespace TrailBlaze.Model.Activity;

/// <summary>
/// An activity's cover as the cover route returns it.
/// </summary>
/// <remarks>
/// One field whichever container holds the bytes (Decision #29): a client is handed a URL that
/// works and never has to know whether it is unsigned or signed. <c>CoverImageBlobPath</c> is
/// deliberately absent for the reason <c>MediaResponse</c> gives — the path names an object only a
/// SAS is supposed to unlock.
/// </remarks>
public sealed record CoverResponse
{
    public required string CoverImageUrl { get; init; }
}
