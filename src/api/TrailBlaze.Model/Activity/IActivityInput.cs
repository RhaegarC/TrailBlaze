namespace TrailBlaze.Model.Activity;

/// <summary>
/// What a caller may send to a create or an update, which is the same field set on both
/// routes — so validation is written once rather than once per request type.
/// </summary>
/// <remarks>
/// Nothing here can attribute an entry to a person or stamp a time: <c>CreatedByUserId</c>,
/// <c>CreatedOn</c>, <c>Id</c> and <c>CoverImageBlobPath</c> have no property to arrive in.
/// </remarks>
public interface IActivityInput
{
    string? Title { get; }

    string? Location { get; }

    DateOnly? ActivityDate { get; }

    string? Description { get; }

    /// <summary>Absent means "not supplied", which is not the same as an invalid value.</summary>
    string? Type { get; }
}
