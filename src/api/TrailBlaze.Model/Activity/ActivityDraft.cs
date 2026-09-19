namespace TrailBlaze.Model.Activity;

/// <summary>
/// The values of an activity as they will be stored: trimmed, with an absent description
/// collapsed to null and the type resolved to its canonical spelling.
/// </summary>
public sealed record ActivityDraft
{
    public required string Title { get; init; }

    public required string Location { get; init; }

    public required DateOnly ActivityDate { get; init; }

    /// <summary>Null rather than empty when the caller sent nothing: no description is a
    /// normal state, and storing both would make every reader decide which it is holding.</summary>
    public string? Description { get; init; }

    public required string Type { get; init; }

    /// <summary>
    /// The stored form of input that has passed <c>IActivityValidationService.Validate</c>,
    /// which is what guarantees the two required strings and the date are present.
    /// </summary>
    public static ActivityDraft From(IActivityInput input) => new()
    {
        Title = input.Title!.Trim(),
        Location = input.Location!.Trim(),
        ActivityDate = input.ActivityDate!.Value,
        Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
        Type = CanonicalType(input.Type),
    };

    /// <summary>Whether a supplied type names one of the closed set. An absent one counts as
    /// supplied-nothing rather than as wrong.</summary>
    public static bool IsKnownType(string? type) =>
        string.IsNullOrWhiteSpace(type)
        || Constant.ActivityType.All.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>The value as stored: the matching constant, or the default when the caller
    /// supplied nothing. Compared case-insensitively so <c>public</c> is accepted, but never
    /// stored — a column holding two spellings of one value is one nothing can compare.</summary>
    public static string CanonicalType(string? type) =>
        Constant.ActivityType.All.FirstOrDefault(
            known => string.Equals(known, type?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? Constant.ActivityType.Default;
}
