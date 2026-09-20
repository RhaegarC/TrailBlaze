namespace TrailBlaze.Service.Test;

using System.Text.Json;
using TrailBlaze.Model;
using TrailBlaze.Model.Activity;

/// <summary>
/// The property set an activity response puts on the wire, asserted by serialising it.
/// </summary>
/// <remarks>
/// A property set rather than a sample response, so a field added later fails here instead of
/// arriving in a payload an anonymous caller can fetch unnoticed. The anonymous shape is the one
/// that matters: a user id or a blob path reaching it is the disclosure this feature exists to
/// prevent, and it is the shape no token is needed to obtain.
/// </remarks>
public sealed class ActivityPayloadTests
{
    /// <summary>The options <c>AddControllers</c> installs, so this is the wire shape rather than
    /// one a test chose.</summary>
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private static readonly DateOnly Date = new(2026, 3, 14);

    private const string SomebodyElse = "another-users-object-id";

    [Fact]
    public void An_anonymous_payload_carries_the_entry_the_creator_and_the_media_count() =>
        Assert.Equal(
            [
                "activityDate", "coverImageUrl", "creatorDisplayName", "description",
                "id", "location", "mediaCount", "title", "type",
            ],
            NamesOf(Response(creatorId: null)));

    [Fact]
    public void A_signed_in_payload_carries_the_creators_id_as_well() =>
        Assert.Equal(
            [
                "activityDate", "coverImageUrl", "createdByUserId", "creatorDisplayName",
                "description", "id", "location", "mediaCount", "title", "type",
            ],
            NamesOf(Response(creatorId: SomebodyElse)));

    [Fact]
    public void An_anonymous_payload_carries_no_user_id() =>
        Assert.DoesNotContain("createdByUserId", NamesOf(Response(creatorId: null)));

    [Fact]
    public void The_response_has_no_blob_path_property() =>
        Assert.DoesNotContain(
            typeof(ActivityResponse).GetProperties(),
            property => property.Name.Contains("Path", StringComparison.Ordinal));

    /// <summary>
    /// Every other media-derived value stays out, and the count is the one exception (Decision
    /// #30). Named as literals rather than derived from a type, because the failure this catches is
    /// a property nobody intended to expose.
    /// </summary>
    [Fact]
    public void The_payload_carries_no_per_item_media_field()
    {
        string[] forbidden =
        [
            "mediaId", "blobPath", "sasUrl", "contentType", "sizeBytes",
            "fileName", "originalFileName", "uploadedByUserId",
        ];

        Assert.Empty(forbidden.Intersect(NamesOf(Response(creatorId: null))));
    }

    [Fact]
    public void An_activity_date_serialises_with_no_time_and_no_offset() =>
        Assert.Contains("\"activityDate\":\"2026-03-14\"", Serialised(Response(creatorId: null)));

    [Fact]
    public void An_entry_with_no_cover_carries_a_null_url_rather_than_omitting_it() =>
        Assert.Contains("\"coverImageUrl\":null", Serialised(Response(creatorId: null)));

    [Fact]
    public void A_media_count_of_zero_is_still_reported() =>
        Assert.Contains("\"mediaCount\":0", Serialised(Response(creatorId: null) with { MediaCount = 0 }));

    private static ActivityResponse Response(string? creatorId) => new()
    {
        Id = "the-activity",
        Title = "Ridge walk",
        Location = "North ridge",
        ActivityDate = Date,
        Description = "A long walk",
        Type = Constant.ActivityType.Public,
        CoverImageUrl = null,
        MediaCount = 3,
        CreatorDisplayName = "Ada",
        CreatedByUserId = creatorId,
    };

    private static string[] NamesOf(ActivityResponse response) =>
        [.. JsonDocument.Parse(Serialised(response)).RootElement
            .EnumerateObject().Select(property => property.Name).Order()];

    private static string Serialised(ActivityResponse response) =>
        JsonSerializer.Serialize(response, Wire);
}
