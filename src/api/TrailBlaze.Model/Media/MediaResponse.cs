namespace TrailBlaze.Model.Media;

using System.Text.Json.Serialization;

/// <summary>
/// One stored item as these routes return it: its metadata, and the URL its bytes are read by.
/// </summary>
/// <remarks>
/// <c>BlobPath</c> is deliberately absent, and the field carrying the address is named <c>Url</c>
/// rather than for the path it points at: what a caller may hold is a short-lived link, not the
/// location of an object in a private container.
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

    /// <summary>The URL the bytes are read by, so a listing needs no follow-up request per item.</summary>
    public required string Url { get; init; }

    /// <summary>When <see cref="Url"/> stops working — the same instant the token carries.</summary>
    public required DateTimeOffset ExpiresOnUtc { get; init; }

    /// <summary>Who contributed it — the field the detail view groups by (Decision #27). Omitted
    /// rather than sent as null to a caller with no token: the <c>users</c> primary key is the
    /// Entra object id, so an anonymous payload names the uploader and does not identify them
    /// (Decision #30).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UploadedByUserId { get; init; }

    /// <summary>The uploader's name, resolved on the way out so a group needs no second request.</summary>
    public string? UploaderDisplayName { get; init; }
}
