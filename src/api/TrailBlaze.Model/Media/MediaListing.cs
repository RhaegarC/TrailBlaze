namespace TrailBlaze.Model.Media;

/// <summary>
/// An activity's media: its items, or the fact that the caller may not see the activity at all.
/// </summary>
/// <remarks>
/// Two answers rather than an outcome with a reason, because there are only two. A caller who may
/// not read the activity is told it does not exist — the media surface must not become a way to
/// probe for a <c>Private</c> entry (PRD "Authentication &amp; authorization").
/// </remarks>
/// <param name="Found">False when the activity is unknown or unreadable to this caller.</param>
/// <param name="Items">The items, empty when <paramref name="Found"/> is false.</param>
public sealed record MediaListing(bool Found, IReadOnlyList<MediaResponse> Items)
{
    /// <summary>No such activity, or one this caller may not read.</summary>
    public static MediaListing NotFound() => new(false, []);

    /// <summary>The activity's items, possibly none.</summary>
    public static MediaListing Of(IReadOnlyList<MediaResponse> items) => new(true, items);
}
