namespace TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// One image or video attached to an activity.
/// </summary>
/// <remarks>
/// The uploader is <see cref="EntityBase.CreatedBy"/>, set from the caller. It is not the activity's
/// creator: media is collaborative (Decision #27), so the two are usually different people.
/// </remarks>
public sealed class Media : EntityBase
{
    /// <summary>The activity it belongs to, as a plain column: the model declares no foreign keys.</summary>
    public string ActivityId { get; set; } = string.Empty;

    /// <summary><c>Image</c> or <c>Video</c>, derived from the content type.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Path within the private <c>media</c> container. Never returned: a reader is handed
    /// a short-lived SAS URL instead (feature 07).</summary>
    public string BlobPath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    /// <summary>The bytes actually stored. A column rather than a blob property, so a listing can
    /// be answered without reaching storage at all.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Kept for display only. It never addresses the blob.</summary>
    public string OriginalFileName { get; set; } = string.Empty;
}
