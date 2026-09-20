namespace TrailBlaze.Model.Activity;

using System.Text.Json.Serialization;

/// <summary>An activity as these routes return it.</summary>
/// <remarks>
/// Carries the creator's display name and a media count, and nothing else about either: no user
/// id, no blob path, no media id, content type, size or file name. The count is the one
/// media-derived value an anonymous caller may be handed (Decision #30).
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

    /// <summary>A plain public URL for a <c>Public</c> activity and a short-lived SAS for a
    /// <c>Shared</c> or <c>Private</c> one, whose cover sits in the private container
    /// (Decision #29). Null when the activity has no cover.</summary>
    public string? CoverImageUrl { get; init; }

    public required int MediaCount { get; init; }

    public string? CreatorDisplayName { get; init; }

    /// <summary>Omitted rather than sent as null to a caller with no token — the field's presence
    /// is what a client reads as permission to offer edit controls.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CreatedByUserId { get; init; }
}
