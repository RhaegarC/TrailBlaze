namespace TrailBlaze.Service.Test;

using System.Text.Json;
using TrailBlaze.Model;
using TrailBlaze.Model.Media;

/// <summary>
/// The property set a media response puts on the wire, asserted by serialising it.
/// </summary>
/// <remarks>
/// A property set rather than a sample response, so a field added later fails here instead of
/// arriving in a payload an anonymous caller can fetch unnoticed. The anonymous shape is the one
/// that matters: the <c>users</c> primary key is the Entra object id, and the media listing became
/// readable without a token, so an id reaching this shape is exactly the disclosure Decision #30
/// forbids.
/// </remarks>
public sealed class MediaPayloadTests
{
    /// <summary>The options <c>AddControllers</c> installs, so this is the wire shape rather than
    /// one a test chose.</summary>
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    private const string SomebodyElse = "another-users-object-id";

    [Fact]
    public void An_anonymous_payload_names_the_uploader_without_identifying_them() =>
        Assert.Equal(
            [
                "contentType", "createdOn", "id", "kind", "originalFileName",
                "sizeBytes", "uploaderDisplayName",
            ],
            NamesOf(Response(uploaderId: null)));

    [Fact]
    public void A_signed_in_payload_carries_the_uploaders_id_as_well() =>
        Assert.Equal(
            [
                "contentType", "createdOn", "id", "kind", "originalFileName",
                "sizeBytes", "uploadedByUserId", "uploaderDisplayName",
            ],
            NamesOf(Response(uploaderId: SomebodyElse)));

    /// <summary>
    /// A withheld id is omitted rather than sent as null: the field's presence is what a client
    /// reads as permission to key a per-uploader control on it.
    /// </summary>
    [Fact]
    public void A_withheld_id_is_absent_rather_than_null() =>
        Assert.DoesNotContain("uploadedByUserId", Serialised(Response(uploaderId: null)));

    [Fact]
    public void The_response_has_no_blob_path_property() =>
        Assert.DoesNotContain(
            typeof(MediaResponse).GetProperties(),
            property => property.Name.Contains("Path", StringComparison.Ordinal));

    private static MediaResponse Response(string? uploaderId) => new()
    {
        Id = "the-item",
        Kind = Constant.MediaKind.Image,
        ContentType = "image/png",
        SizeBytes = 32,
        OriginalFileName = "one.png",
        CreatedOn = new DateTimeOffset(2026, 3, 14, 8, 0, 0, TimeSpan.Zero),
        UploadedByUserId = uploaderId,
        UploaderDisplayName = "Ada",
    };

    private static string[] NamesOf(MediaResponse response) =>
        [.. JsonDocument.Parse(Serialised(response)).RootElement
            .EnumerateObject().Select(property => property.Name).Order()];

    private static string Serialised(MediaResponse response) =>
        JsonSerializer.Serialize(response, Wire);
}
