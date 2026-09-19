namespace TrailBlaze.Model.Activity;

/// <summary>
/// What a caller may send to a create or an update; no field attributes an entry or stamps a time.
/// </summary>
public interface IActivityInput
{
    string? Title { get; }

    string? Location { get; }

    DateOnly? ActivityDate { get; }

    string? Description { get; }

    /// <summary>
    /// Absent means "not supplied", which is not the same as an invalid value.
    /// </summary>
    string? Type { get; }
}
