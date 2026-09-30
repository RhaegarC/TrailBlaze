namespace TrailBlaze.Model.Media;

using System.Text.Json.Serialization;

/// <summary>
/// One stored item as these routes return it: metadata, and nothing a caller could fetch.
/// </summary>
/// <remarks>
/// <c>BlobPath</c> is deliberately absent. The path names an object in the private container, so
/// returning it would hand out an address that only a SAS is supposed to unlock — the bytes are
/// reached through feature 07's endpoint, and this payload is what tells a client there is
/// something to reach.
/// </remarks>
public sealed record MediaResponse
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    public required string ContentType { get; init; }

    public required long SizeBytes { get; init; }

    public required string OriginalFileName { get; init; }

    /// <summary>When it was uploaded, from the row's audit stamp.</summary>
    public required DateTimeOffset CreatedOn { get; init; }

    /// <summary>Who contributed it — the field the detail view groups by (Decision #27). Omitted
    /// rather than sent as null to a caller with no token: the <c>users</c> primary key is the
    /// Entra object id, so an anonymous payload names the uploader and does not identify them
    /// (Decision #30).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UploadedByUserId { get; init; }

    /// <summary>The uploader's name, resolved on the way out so a group needs no second request.</summary>
    public string? UploaderDisplayName { get; init; }
}
