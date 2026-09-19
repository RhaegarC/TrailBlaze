namespace TrailBlaze.Model.Activity;

/// <summary>
/// The values of an activity as they will be stored.
/// </summary>
public sealed record ActivityDraft
{
    public required string Title { get; init; }

    public required string Location { get; init; }

    public required DateOnly ActivityDate { get; init; }

    /// <summary>
    /// Null rather than empty when the caller sent nothing; no description is a normal state.
    /// </summary>
    public string? Description { get; init; }

    public required string Type { get; init; }

    /// <summary>
    /// The stored form of input that has passed validation.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static ActivityDraft From(IActivityInput input) => new()
    {
        Title = input.Title!.Trim(),
        Location = input.Location!.Trim(),
        ActivityDate = input.ActivityDate!.Value,
        Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
        Type = CanonicalType(input.Type),
    };

    /// <summary>
    /// Whether a supplied type names one of the closed set; an absent one is unsupplied, not wrong.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsKnownType(string? type) =>
        string.IsNullOrWhiteSpace(type)
        || Constant.ActivityType.All.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The value as stored: the matching constant, or the default when the caller supplied none.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string CanonicalType(string? type) =>
        Constant.ActivityType.All.FirstOrDefault(
            known => string.Equals(known, type?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? Constant.ActivityType.Default;
}
