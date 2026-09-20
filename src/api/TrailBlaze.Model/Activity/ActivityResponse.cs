namespace TrailBlaze.Model.Activity;

/// <summary>An activity as these routes return it.</summary>
/// <remarks>
/// Carries no creator and no <c>CreatedOn</c>. Feature 05 decides which caller
/// may be told the creator's identity — an anonymous response may not be — so this type holds
/// the field set both callers share rather than a set one of them would have to be trimmed
/// out of later.
/// </remarks>
public sealed record ActivityResponse
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Location { get; init; }

    /// <summary>Serialises as <c>yyyy-MM-dd</c>, with no time and no offset.</summary>
    public required DateOnly ActivityDate { get; init; }

    public string? Description { get; init; }

    public required string Type { get; init; }

    /// <summary>The stored path, never a URL. Feature 08 fills it in; nothing here writes it, and a
    /// list response withholds it from a caller who has no token.</summary>
    public string? CoverImageBlobPath { get; init; }
}
