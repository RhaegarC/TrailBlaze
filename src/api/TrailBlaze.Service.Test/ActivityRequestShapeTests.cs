namespace TrailBlaze.Service.Test;

using TrailBlaze.Model.Activity;

/// <summary>
/// The values a caller must not be able to set, asserted as an absence of properties rather
/// than as a check that could be removed.
/// </summary>
/// <remarks>
/// "The service ignores a body-supplied creator" is a rule a reviewer has to believe; "there is
/// no property for a creator to arrive in" is one they can read. Both requests are covered
/// because a field added to one is the mistake the other would not catch.
/// </remarks>
public sealed class ActivityRequestShapeTests
{
    [Theory]
    [InlineData(typeof(CreateActivityRequest))]
    [InlineData(typeof(UpdateActivityRequest))]
    public void A_request_offers_nothing_a_caller_must_not_supply(Type request)
    {
        foreach (string forbidden in Forbidden)
        {
            Assert.DoesNotContain(
                request.GetProperties(),
                property => string.Equals(property.Name, forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Attribution, the audit stamps, the key, the soft-delete flag and the cover. The cover is
    /// on the list because feature 08 writes it from its own route, so a field here would be a
    /// second writer for a value two features would then disagree about.
    /// </summary>
    private static readonly string[] Forbidden =
    [
        "Id",
        "CreatedBy",
        "CreatedByUserId",
        "CreatedOn",
        "LastModifiedBy",
        "LastModifiedOn",
        "IsDeleted",
        "CoverImageBlobPath",
    ];
}
