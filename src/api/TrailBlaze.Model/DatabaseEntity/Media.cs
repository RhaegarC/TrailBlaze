namespace TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// One image or video attached to an activity, and who attached it.
/// </summary>
/// <remarks>
/// The uploader is a column of its own rather than a read of the activity's creator, because the
/// two answer different questions: who logged the day, and who contributed this picture. Media is
/// collaborative (Decision #27), so they are usually different people.
/// </remarks>
public sealed class Media : EntityBase
{
    /// <summary>The activity it belongs to, as a plain column: the model declares no foreign keys.</summary>
    public string ActivityId { get; set; } = string.Empty;

    /// <summary>The caller who uploaded it, never a value from the request body.</summary>
    public string UploadedByUserId { get; set; } = string.Empty;

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
