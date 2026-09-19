namespace TrailBlaze.Interface.Service;

using TrailBlaze.Model.Activity;

/// <summary>
/// The rules an activity's fields must satisfy, decided from the request alone.
/// </summary>
/// <remarks>
/// A type of its own so those rules can be asserted without a store — which is what keeps them
/// out of reach of a double pretending to be a database
/// ([testing-and-tdd.md](../../docs/testing-and-tdd.md)).
/// </remarks>
public interface IActivityValidationService
{
    /// <summary>
    /// Checks the fields a caller supplied.
    /// </summary>
    /// <param name="input">The values from either the create or the update body.</param>
    /// <returns>Reasons keyed by field, empty when the input is valid. An absent
    /// <c>Type</c> is valid: it means the caller supplied none.</returns>
    IReadOnlyDictionary<string, string[]> Validate(IActivityInput input);
}
