namespace TrailBlaze.Model.Activity;

/// <summary>The body of <c>PUT /api/activity/{id}</c>.</summary>
public sealed record UpdateActivityRequest : IActivityInput
{
    public string? Title { get; init; }

    public string? Location { get; init; }

    public DateOnly? ActivityDate { get; init; }

    public string? Description { get; init; }

    /// <summary>The one field an omission does not default. Absent leaves the stored value
    /// alone: a visibility change is a disclosure, so it has to be asked for rather than fall
    /// out of a field the client did not know about.</summary>
    public string? Type { get; init; }
}
