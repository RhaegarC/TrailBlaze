namespace TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// One logged entry: where someone went, when, and who may read it.
/// </summary>
public sealed class Activity : EntityBase
{
    public string Title { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// A calendar date. No time, no timezone — see PRD Decision #25.
    /// </summary>
    public DateOnly ActivityDate { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// <c>Public</c>, <c>Shared</c> or <c>Private</c>. Stored here, evaluated by features 05 and 09.
    /// </summary>
    public string Type { get; set; } = Constant.ActivityType.Default;

    /// <summary>
    /// Set only by the cover upload route (feature 08), never by a create or update body.
    /// </summary>
    public string? CoverImageBlobPath { get; set; }

    /// <summary>
    /// The caller's id as a plain column: the model declares no foreign keys.
    /// </summary>
    public string CreatedByUserId { get; set; } = string.Empty;
}
