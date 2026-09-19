namespace TrailBlaze.Model.Activity;

/// <summary>The body of <c>POST /api/activities</c>.</summary>
public sealed record CreateActivityRequest : IActivityInput
{
    public string? Title { get; init; }

    public string? Location { get; init; }

    public DateOnly? ActivityDate { get; init; }

    public string? Description { get; init; }

    /// <summary>Omitted means <c>Public</c>, so a client that does not send the field keeps
    /// working.</summary>
    public string? Type { get; init; }
}
