namespace TrailBlaze.Model.Activity;

/// <summary>
/// One page of activities, and what a caller needs to ask for the next one.
/// </summary>
public sealed record ActivityPage
{
    /// <summary>
    /// The page's items, in the order the store returned them.
    /// </summary>
    public required IReadOnlyList<ActivityResponse> Items { get; init; }

    /// <summary>
    /// The zero-based index of this page.
    /// </summary>
    public required int Page { get; init; }

    /// <summary>
    /// The size actually applied, which differs from the request when it was clamped.
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// The number of activities the caller may read — not the number of rows in the table.
    /// </summary>
    public required int Total { get; init; }
}
