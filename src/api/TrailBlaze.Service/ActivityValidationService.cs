namespace TrailBlaze.Service;

using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.Activity;

/// <summary>
/// The field rules an activity must satisfy, read from the bounds the columns carry.
/// </summary>
/// <remarks>
/// The lengths are the column lengths, not a second set of numbers: a value this service accepts
/// and the column cannot hold is a 500 at the insert rather than a 400 at the door.
/// </remarks>
public sealed class ActivityValidationService : IActivityValidationService
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string[]> Validate(IActivityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new Dictionary<string, string[]>();

        AddIf(errors, nameof(input.Title), Missing(input.Title, Constant.Message.TitleRequired));
        AddIf(errors, nameof(input.Title), TooLong(input.Title, Constant.ActivityField.TitleLength, Constant.Message.TitleTooLong));

        AddIf(errors, nameof(input.Location), Missing(input.Location, Constant.Message.LocationRequired));
        AddIf(errors, nameof(input.Location), TooLong(input.Location, Constant.ActivityField.LocationLength, Constant.Message.LocationTooLong));

        if (input.ActivityDate is null)
        {
            errors[nameof(input.ActivityDate)] = [Constant.Message.ActivityDateRequired];
        }

        // An absent type is a caller who supplied none, which is not a value to reject — the
        // default answers it. Only a value that names something is checked against the set.
        if (!ActivityDraft.IsKnownType(input.Type))
        {
            errors[nameof(input.Type)] = [Constant.Message.TypeNotAllowed];
        }

        return errors;
    }

    /// <summary>Whitespace is absence, not a value: it reaches the column as empty once trimmed.</summary>
    private static string? Missing(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? message : null;

    /// <summary>Inclusive, and measured on the trimmed value — the length the column must hold.</summary>
    private static string? TooLong(string? value, int cap, string message) =>
        value is not null && value.Trim().Length > cap ? message : null;

    /// <summary>One field, two possible objections, so a blank field one character too long
    /// reports both reasons a caller can act on.</summary>
    private static void AddIf(Dictionary<string, string[]> errors, string field, string? message)
    {
        if (message is null)
        {
            return;
        }

        errors[field] = errors.TryGetValue(field, out string[]? existing)
            ? [.. existing, message]
            : [message];
    }
}
