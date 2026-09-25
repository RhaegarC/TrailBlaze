namespace TrailBlaze.Service.Test;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq.Expressions;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Model.Media;

/// <summary>
/// What an activity's media may be, who may contribute it, and who may remove it.
/// </summary>
/// <remarks>
/// The rules exercised here are decided from the request and the activity alone, which is what makes
/// them answerable without a store. The one claim this tier cannot make is that a blob really landed
/// or really went: that needs a real account, and it is asserted in
/// <c>TrailBlaze.Repository.Test</c>'s container tier instead.
/// </remarks>
public sealed class MediaServiceTests
{
    private const string Owner = "the-owners-object-id";

    private const string Contributor = "a-contributors-object-id";

    private const string Stranger = "a-strangers-object-id";

    private const string Admin = "the-administrators-object-id";

    private const string ActivityId = "the-activity";

    private static byte[] Bytes => "trail-blaze"u8.ToArray();

    // ---- What may be stored ---------------------------------------------------------------

    [Theory]
    [InlineData("image/jpeg", Constant.MediaKind.Image)]
    [InlineData("image/png", Constant.MediaKind.Image)]
    [InlineData("image/webp", Constant.MediaKind.Image)]
    [InlineData("image/gif", Constant.MediaKind.Image)]
    [InlineData("video/mp4", Constant.MediaKind.Video)]
    [InlineData("video/quicktime", Constant.MediaKind.Video)]
    [InlineData("IMAGE/PNG", Constant.MediaKind.Image)]
    public async Task An_admitted_type_is_stored_under_the_kind_it_belongs_to(
        string contentType, string kind)
    {
        var harness = new Harness();

        MediaOutcome outcome = await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), contentType, Bytes.Length, "ridge.png");

        Assert.Equal(MediaOutcomeKind.Uploaded, outcome.Kind);
        Assert.Equal(kind, harness.Repository.Inserted.Single().Kind);
        Assert.Equal(contentType, harness.Repository.Inserted.Single().ContentType);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("video/webm")]
    [InlineData("image/bmp")]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_type_neither_allowlist_admits_is_refused_and_writes_nothing(string? contentType)
    {
        var harness = new Harness();

        MediaOutcome outcome = await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), contentType, Bytes.Length, "ridge.png");

        Assert.Equal(MediaOutcomeKind.Rejected, outcome.Kind);
        Assert.Equal(Constant.Message.MediaTypeNotAllowed, outcome.Errors!["file"].Single());
        Assert.Empty(harness.Repository.Inserted);
        Assert.Empty(harness.Storage.Uploads);
    }

    [Fact]
    public async Task An_image_exactly_at_the_cap_is_accepted()
    {
        var harness = new Harness();

        MediaOutcome outcome = await harness.Service.UploadAsync(
            ActivityId,
            new MemoryStream(Bytes),
            "image/png",
            Constant.Upload.ImageSizeCapBytes,
            "ridge.png");

        Assert.Equal(MediaOutcomeKind.Uploaded, outcome.Kind);
    }

    [Fact]
    public async Task An_image_one_byte_over_the_cap_is_refused()
    {
        var harness = new Harness();

        MediaOutcome outcome = await harness.Service.UploadAsync(
            ActivityId,
            new MemoryStream(Bytes),
            "image/png",
            Constant.Upload.ImageSizeCapBytes + 1,
            "ridge.png");

        Assert.Equal(MediaOutcomeKind.Rejected, outcome.Kind);
        Assert.Equal(Constant.Message.ImageTooLarge, outcome.Errors!["file"].Single());
        Assert.Empty(harness.Storage.Uploads);
    }

    [Fact]
    public async Task A_video_exactly_at_the_cap_is_accepted()
    {
        var harness = new Harness();

        MediaOutcome outcome = await harness.Service.UploadAsync(
            ActivityId,
            new MemoryStream(Bytes),
            "video/mp4",
            Constant.Upload.VideoSizeCapBytes,
            "ridge.mp4");

        Assert.Equal(MediaOutcomeKind.Uploaded, outcome.Kind);
    }

    [Fact]
    public async Task A_video_one_byte_over_the_cap_is_refused()
    {
        var harness = new Harness();

        MediaOutcome outcome = await harness.Service.UploadAsync(
            ActivityId,
            new MemoryStream(Bytes),
            "video/mp4",
            Constant.Upload.VideoSizeCapBytes + 1,
            "ridge.mp4");

        Assert.Equal(MediaOutcomeKind.Rejected, outcome.Kind);
        Assert.Equal(Constant.Message.VideoTooLarge, outcome.Errors!["file"].Single());
    }

    /// <summary>
    /// The two directions of one claim: the cap follows the kind, so a single shared cap would fail
    /// both. A video at 11 MB is over the image cap and admissible; an image at 11 MB is under the
    /// video cap and refused.
    /// </summary>
    [Fact]
    public async Task Each_kind_is_held_to_its_own_cap_and_not_the_other_one()
    {
        var harness = new Harness();
        long overTheImageCap = Constant.Upload.ImageSizeCapBytes + 1;

        MediaOutcome video = await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "video/mp4", overTheImageCap, "ridge.mp4");

        MediaOutcome image = await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", overTheImageCap, "ridge.png");

        Assert.Equal(MediaOutcomeKind.Uploaded, video.Kind);
        Assert.Equal(MediaOutcomeKind.Rejected, image.Kind);
    }

    // ---- What is stored -------------------------------------------------------------------

    [Fact]
    public async Task An_upload_is_attributed_to_the_caller_and_not_to_the_activity_owner()
    {
        var harness = new Harness(caller: Contributor);

        await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge.png");

        Assert.Equal(Contributor, harness.Repository.Inserted.Single().CreatedBy);
    }

    [Fact]
    public async Task The_item_carries_the_name_the_client_sent()
    {
        var harness = new Harness();

        await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge walk.png");

        Assert.Equal("ridge walk.png", harness.Repository.Inserted.Single().OriginalFileName);
        Assert.Equal(Bytes.Length, harness.Repository.Inserted.Single().SizeBytes);
    }

    [Fact]
    public async Task The_blob_goes_to_the_private_container_under_the_activity()
    {
        var harness = new Harness();

        await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge.png");

        (string container, string path, string contentType) = harness.Storage.Uploads.Single();

        Assert.Equal(Constant.StorageContainer.Media, container);
        Assert.StartsWith($"{ActivityId}/", path, StringComparison.Ordinal);
        Assert.Equal("image/png", contentType);

        // The row names the object that was written, or the item is addressable by nothing.
        Assert.Equal(path, harness.Repository.Inserted.Single().BlobPath);
    }

    [Fact]
    public async Task Every_upload_gets_a_fresh_path()
    {
        var harness = new Harness();

        await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge.png");
        await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge.png");

        string[] paths = [.. harness.Storage.Uploads.Select(upload => upload.Path)];

        Assert.NotEqual(paths[0], paths[1]);
    }

    // ---- The item limit -------------------------------------------------------------------

    [Fact]
    public async Task An_activity_at_its_limit_is_refused_before_the_blob_is_written()
    {
        var harness = new Harness();
        harness.Repository.RoomForMore = false;

        MediaOutcome outcome = await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge.png");

        Assert.Equal(MediaOutcomeKind.LimitReached, outcome.Kind);
        Assert.Empty(harness.Storage.Uploads);
        Assert.Empty(harness.Repository.Inserted);
    }

    /// <summary>
    /// The cap counted is one contributor's items on one activity, which is neither the activity's
    /// total nor the contributor's everywhere: the predicate is run here against three rows rather
    /// than described.
    /// </summary>
    [Fact]
    public async Task The_limit_is_counted_over_the_contributor_and_the_activity()
    {
        var harness = new Harness();

        await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge.png");

        Func<Media, bool> counted = harness.Repository.Counted!.Compile();

        Assert.Equal(Constant.MediaLimit.PerContributorPerActivity, harness.Repository.Cap);
        Assert.True(counted(Item("mine", Contributor)));
        Assert.False(counted(Item("theirs", Stranger)));
        Assert.False(counted(new Media { ActivityId = "another-activity", CreatedBy = Contributor }));
    }

    // ---- A blob that never landed ---------------------------------------------------------

    [Fact]
    public async Task A_blob_that_never_landed_undoes_its_row()
    {
        var harness = new Harness();
        harness.Storage.RefuseUploads = true;

        await Assert.ThrowsAsync<NotSupportedException>(() => harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge.png"));

        Assert.Equal(
            [harness.Repository.Inserted.Single().Id], harness.Repository.DeletedMedia);
    }

    // ---- Who may contribute ---------------------------------------------------------------

    [Theory]
    [InlineData(null, Constant.ActivityType.Public, Owner, MediaOutcomeKind.NoCaller)]
    [InlineData(Contributor, Constant.ActivityType.Public, Owner, MediaOutcomeKind.Uploaded)]
    [InlineData(Contributor, Constant.ActivityType.Shared, Owner, MediaOutcomeKind.Uploaded)]
    [InlineData(Contributor, Constant.ActivityType.Private, Owner, MediaOutcomeKind.NotFound)]
    [InlineData(Owner, Constant.ActivityType.Private, Owner, MediaOutcomeKind.Uploaded)]
    [InlineData(Contributor, Constant.ActivityType.Shared, Contributor, MediaOutcomeKind.Uploaded)]
    public async Task Any_caller_who_can_read_the_activity_may_contribute_to_it(
        string? caller, string type, string owner, MediaOutcomeKind expected)
    {
        var harness = new Harness(caller);
        harness.Repository.Activity = Activity(type, owner);

        MediaOutcome outcome = await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge.png");

        Assert.Equal(expected, outcome.Kind);
    }

    /// <summary>
    /// Decision #27's third principal, and the reason the override is the role's rather than a
    /// widening of the read rule: an administrator may contribute to an entry they could not
    /// otherwise read.
    /// </summary>
    [Fact]
    public async Task An_administrator_may_contribute_to_an_entry_they_could_not_read()
    {
        var harness = new Harness(caller: Admin, isAdmin: true);
        harness.Repository.Activity = Activity(Constant.ActivityType.Private, Owner);

        MediaOutcome outcome = await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge.png");

        Assert.Equal(MediaOutcomeKind.Uploaded, outcome.Kind);
    }

    /// <summary>
    /// The same id in the same request, judged by the row instead: the only thing separating the
    /// two answers above and here is what <c>users.Role</c> says.
    /// </summary>
    [Fact]
    public async Task The_same_caller_without_the_admin_role_cannot_contribute_to_that_entry()
    {
        var harness = new Harness(caller: Admin);
        harness.Repository.Activity = Activity(Constant.ActivityType.Private, Owner);

        MediaOutcome outcome = await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge.png");

        Assert.Equal(MediaOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task An_activity_that_names_nothing_is_refused_and_writes_nothing()
    {
        var harness = new Harness();
        harness.Repository.Activity = null;

        MediaOutcome outcome = await harness.Service.UploadAsync(
            ActivityId, new MemoryStream(Bytes), "image/png", Bytes.Length, "ridge.png");

        Assert.Equal(MediaOutcomeKind.NotFound, outcome.Kind);
        Assert.Empty(harness.Storage.Uploads);
        Assert.Empty(harness.Repository.Inserted);
    }

    // ---- Who may read ---------------------------------------------------------------------

    [Theory]
    [InlineData(Contributor, Constant.ActivityType.Public, Owner, true)]
    [InlineData(Contributor, Constant.ActivityType.Shared, Owner, true)]
    [InlineData(Contributor, Constant.ActivityType.Private, Owner, false)]
    [InlineData(Owner, Constant.ActivityType.Private, Owner, true)]
    [InlineData(Contributor, Constant.ActivityType.Shared, Contributor, true)]
    public async Task Reading_an_activity_a_caller_may_not_see_is_not_found(
        string? caller, string type, string owner, bool found)
    {
        var harness = new Harness(caller);
        harness.Repository.Activity = Activity(type, owner);

        MediaListing listing = await harness.Service.ListAsync(ActivityId);

        Assert.Equal(found, listing.Found);
    }

    /// <summary>
    /// No token reads nothing rather than everything: the media surface must not become a way in.
    /// </summary>
    [Fact]
    public async Task Listing_without_a_caller_is_not_found()
    {
        var harness = new Harness(caller: null);

        MediaListing listing = await harness.Service.ListAsync(ActivityId);

        Assert.False(listing.Found);
    }

    [Fact]
    public async Task A_listing_names_each_uploader()
    {
        var harness = new Harness();
        harness.Repository.Items = [Item("one", Contributor), Item("two", Stranger)];
        harness.Repository.Users =
        [
            new User { Id = Contributor, DisplayName = "Ada" },
            new User { Id = Stranger, DisplayName = "Grace" },
        ];

        MediaListing listing = await harness.Service.ListAsync(ActivityId);

        Assert.Equal(
            ["Ada", "Grace"],
            listing.Items.Select(item => item.UploaderDisplayName));
    }

    /// <summary>
    /// A row whose uploader cannot be found still lists: the item is there, and a missing name is
    /// not a reason to hide the bytes the caller came for.
    /// </summary>
    [Fact]
    public async Task An_item_whose_uploader_has_no_row_still_lists()
    {
        var harness = new Harness();
        harness.Repository.Items = [Item("one", Contributor)];

        MediaListing listing = await harness.Service.ListAsync(ActivityId);

        Assert.Single(listing.Items);
        Assert.Null(listing.Items[0].UploaderDisplayName);
    }

    [Fact]
    public async Task A_listing_is_oldest_first()
    {
        var harness = new Harness();
        harness.Repository.Items =
        [
            Item("late", Contributor, new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero)),
            Item("early", Contributor, new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero)),
        ];

        MediaListing listing = await harness.Service.ListAsync(ActivityId);

        Assert.Equal(["early", "late"], listing.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task A_listing_never_carries_a_blob_path()
    {
        var harness = new Harness();
        harness.Repository.Items = [Item("one", Contributor)];

        MediaListing listing = await harness.Service.ListAsync(ActivityId);

        Assert.DoesNotContain(
            typeof(MediaResponse).GetProperties(),
            property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("one", listing.Items[0].Id);
    }

    // ---- Who may remove -------------------------------------------------------------------

    [Theory]
    [InlineData(Contributor, Contributor, MediaOutcomeKind.Deleted)]
    [InlineData(Owner, Contributor, MediaOutcomeKind.Forbidden)]
    [InlineData(Stranger, Contributor, MediaOutcomeKind.Forbidden)]
    [InlineData(null, Contributor, MediaOutcomeKind.NoCaller)]
    public async Task The_uploader_may_remove_an_item_and_the_entries_owner_may_not(
        string? caller, string uploader, MediaOutcomeKind expected)
    {
        var harness = new Harness(caller);
        harness.Repository.Activity = Activity(Constant.ActivityType.Shared, Owner);
        harness.Repository.Item = Item("one", uploader);

        MediaOutcome outcome = await harness.Service.DeleteAsync("one");

        Assert.Equal(expected, outcome.Kind);
    }

    /// <summary>
    /// The administrator is the other principal, and the only one who need not be the uploader to
    /// remove an item.
    /// </summary>
    [Fact]
    public async Task An_administrator_may_remove_an_item_they_did_not_upload()
    {
        var harness = new Harness(caller: Admin, isAdmin: true);
        harness.Repository.Activity = Activity(Constant.ActivityType.Shared, Owner);
        harness.Repository.Item = Item("one", Contributor);

        MediaOutcome outcome = await harness.Service.DeleteAsync("one");

        Assert.Equal(MediaOutcomeKind.Deleted, outcome.Kind);
    }

    /// <summary>
    /// A signed-in caller is not thereby permitted: the third principal is nobody, and this is the
    /// cell that keeps the two above from reading as though any token were enough.
    /// </summary>
    [Fact]
    public async Task A_signed_in_caller_who_is_neither_the_uploader_nor_an_admin_is_forbidden()
    {
        var harness = new Harness(caller: Stranger);
        harness.Repository.Activity = Activity(Constant.ActivityType.Shared, Owner);
        harness.Repository.Item = Item("one", Contributor);

        MediaOutcome outcome = await harness.Service.DeleteAsync("one");

        Assert.Equal(MediaOutcomeKind.Forbidden, outcome.Kind);
        Assert.Empty(harness.Storage.Deleted);
        Assert.Empty(harness.Repository.DeletedMedia);
    }

    [Fact]
    public async Task Removing_an_item_removes_its_row_and_then_its_blob()
    {
        var harness = new Harness();
        harness.Repository.Item = Item("one", Contributor);

        MediaOutcome outcome = await harness.Service.DeleteAsync("one");

        Assert.Equal(MediaOutcomeKind.Deleted, outcome.Kind);
        Assert.Equal(["one"], harness.Repository.DeletedMedia);
        Assert.Equal(["blobs/one"], harness.Storage.Deleted);
    }

    [Fact]
    public async Task An_item_that_names_nothing_removes_nothing()
    {
        var harness = new Harness();
        harness.Repository.Item = null;

        MediaOutcome outcome = await harness.Service.DeleteAsync("one");

        Assert.Equal(MediaOutcomeKind.NotFound, outcome.Kind);
        Assert.Empty(harness.Storage.Deleted);
    }

    // ---- Minting a read URL ---------------------------------------------------------------

    /// <summary>
    /// The security hot spot of the media path, and the one claim a storage double is still
    /// sanctioned for.
    /// </summary>
    /// <remarks>
    /// The count is the whole of the assertion: one recording double, zero interactions. It can be
    /// made no other way — a real backend records nothing, so "the refusal came before any blob
    /// operation" is invisible to the container tier, which can only see what did happen. This
    /// double stands in for no storage behaviour and supports no other claim.
    /// </remarks>
    [Fact]
    public async Task An_unauthenticated_caller_is_refused_before_storage_is_reached()
    {
        var harness = new Harness(caller: null);
        var storage = new NeverReachedStorage();

        MediaUrlOutcome outcome = await harness.ServiceWith(storage).CreateReadUrlAsync("one");

        Assert.Equal(MediaUrlOutcomeKind.NoCaller, outcome.Kind);
        Assert.Empty(storage.Reached);
    }

    /// <summary>
    /// The same count for the caller who is signed in but may not read the entry: a <c>Private</c>
    /// activity's media is not mintable by a stranger, and the refusal precedes the mint exactly as
    /// the anonymous one does.
    /// </summary>
    [Fact]
    public async Task A_caller_who_may_not_read_the_entry_is_refused_before_storage_is_reached()
    {
        var harness = new Harness(caller: Contributor);
        harness.Repository.Activity = Activity(Constant.ActivityType.Private, Owner);

        // The item exists, so the refusal is the visibility gate rather than a row that was absent
        // anyway — without this the test would pass on the lookup missing.
        harness.Repository.Item = Item("one", Contributor);
        var storage = new NeverReachedStorage();

        MediaUrlOutcome outcome = await harness.ServiceWith(storage).CreateReadUrlAsync("one");

        Assert.Equal(MediaUrlOutcomeKind.NotFound, outcome.Kind);
        Assert.Empty(storage.Reached);
    }

    /// <summary>
    /// An id that names nothing is answered as absent, and the answer costs no mint either: whether
    /// the row exists at all is the fact being withheld.
    /// </summary>
    [Fact]
    public async Task An_item_that_names_nothing_mints_nothing()
    {
        var harness = new Harness();
        harness.Repository.Item = null;

        MediaUrlOutcome outcome = await harness.Service.CreateReadUrlAsync("one");

        Assert.Equal(MediaUrlOutcomeKind.NotFound, outcome.Kind);
        Assert.Empty(harness.Storage.ReadUrls);
    }

    /// <summary>
    /// The route has no authorization rule of its own: it reaches the same matrix every other read
    /// does, which is what keeps a private entry's media from being mintable by a signed-in
    /// stranger.
    /// </summary>
    [Theory]
    [InlineData(Contributor, Constant.ActivityType.Public, Owner, false, MediaUrlOutcomeKind.Minted)]
    [InlineData(Contributor, Constant.ActivityType.Shared, Owner, false, MediaUrlOutcomeKind.Minted)]
    [InlineData(Contributor, Constant.ActivityType.Private, Owner, false, MediaUrlOutcomeKind.NotFound)]
    [InlineData(Owner, Constant.ActivityType.Private, Owner, false, MediaUrlOutcomeKind.Minted)]
    [InlineData(Contributor, Constant.ActivityType.Shared, Contributor, false, MediaUrlOutcomeKind.Minted)]
    [InlineData(Admin, Constant.ActivityType.Private, Owner, true, MediaUrlOutcomeKind.Minted)]
    public async Task A_url_is_minted_for_a_caller_who_may_read_the_entry(
        string? caller, string type, string owner, bool isAdmin, MediaUrlOutcomeKind expected)
    {
        var harness = new Harness(caller, isAdmin);
        harness.Repository.Activity = Activity(type, owner);
        harness.Repository.Item = Item("one", Contributor);

        MediaUrlOutcome outcome = await harness.Service.CreateReadUrlAsync("one");

        Assert.Equal(expected, outcome.Kind);
    }

    /// <summary>
    /// The target is the row's own blob, in the private container — one object, not the container.
    /// </summary>
    [Fact]
    public async Task The_url_is_signed_for_the_items_own_blob_in_the_private_container()
    {
        var harness = new Harness();
        harness.Repository.Item = Item("one", Contributor);

        MediaUrlOutcome outcome = await harness.Service.CreateReadUrlAsync("one");

        (string container, string path, _) = Assert.Single(harness.Storage.ReadUrls);

        Assert.Equal(MediaUrlOutcomeKind.Minted, outcome.Kind);
        Assert.Equal(Constant.StorageContainer.Media, container);
        Assert.Equal("blobs/one", path);
        Assert.Equal("https://signed.invalid/media/blobs/one", outcome.Url!.Url);
    }

    /// <summary>
    /// The instant the client is told is the instant the token carries.
    /// </summary>
    /// <remarks>
    /// This is the half of that claim the service tier can make: what it reports is what it handed
    /// to storage. The other half — that storage signs the instant it is handed — needs a real
    /// account and is asserted in <c>TrailBlaze.Repository.Test</c>. Neither test is the whole
    /// claim, and this seam is the reason.
    /// </remarks>
    [Fact]
    public async Task The_expiry_reported_is_the_instant_handed_to_storage()
    {
        var harness = new Harness();
        harness.Repository.Item = Item("one", Contributor);

        MediaUrlOutcome outcome = await harness.Service.CreateReadUrlAsync("one");

        (_, _, DateTimeOffset handedToStorage) = Assert.Single(harness.Storage.ReadUrls);

        Assert.Equal(handedToStorage, outcome.Url!.ExpiresOnUtc);
    }

    /// <summary>
    /// The window is bounded at both ends, which is the whole of the control on a bearer link: a
    /// window of a year would satisfy "expires eventually" and leak the bytes.
    /// </summary>
    [Fact]
    public async Task The_expiry_is_in_the_future_and_within_the_cap()
    {
        var harness = new Harness();
        harness.Repository.Item = Item("one", Contributor);

        MediaUrlOutcome outcome = await harness.Service.CreateReadUrlAsync("one");

        Assert.InRange(
            outcome.Url!.ExpiresOnUtc - DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1),
            SignedUrlLifetime.Maximum);
    }

    /// <summary>
    /// A configured window longer than the cap is clamped to it rather than honoured.
    /// </summary>
    [Fact]
    public async Task A_configured_window_longer_than_the_cap_is_clamped()
    {
        var harness = new Harness(mediaUrlTtl: TimeSpan.FromHours(5));
        harness.Repository.Item = Item("one", Contributor);

        await harness.Service.CreateReadUrlAsync("one");

        // Read off what storage was handed rather than off the response: this is the policy's own
        // output, and "the response repeats it" is a separate claim with a test of its own.
        (_, _, DateTimeOffset expiresOn) = Assert.Single(harness.Storage.ReadUrls);

        Assert.InRange(
            expiresOn - DateTimeOffset.UtcNow,
            SignedUrlLifetime.Maximum - TimeSpan.FromSeconds(1),
            SignedUrlLifetime.Maximum);
    }

    /// <summary>
    /// A configured window that is not positive is the absence of a setting rather than an
    /// instruction to mint a dead link, so the default answers for it.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public async Task A_configured_window_that_is_not_positive_falls_back_to_the_default(int minutes)
    {
        var harness = new Harness(mediaUrlTtl: TimeSpan.FromMinutes(minutes));
        harness.Repository.Item = Item("one", Contributor);

        await harness.Service.CreateReadUrlAsync("one");

        (_, _, DateTimeOffset expiresOn) = Assert.Single(harness.Storage.ReadUrls);

        Assert.InRange(
            expiresOn - DateTimeOffset.UtcNow,
            SignedUrlLifetime.Default - TimeSpan.FromSeconds(1),
            SignedUrlLifetime.Default);
    }

    /// <summary>
    /// The URL is a credential, so it is answered to the caller who asked and written nowhere:
    /// anything that logged it would put a working link to a private blob into the log sink.
    /// </summary>
    [Fact]
    public async Task The_minted_url_is_never_written_to_the_log()
    {
        var harness = new Harness();
        harness.Repository.Item = Item("one", Contributor);
        var logger = new RecordingLogger();
        MediaService service = harness.ServiceWith(harness.Storage, logger);

        MediaUrlOutcome outcome = await service.CreateReadUrlAsync("one");

        string url = outcome.Url!.Url;
        Assert.NotEmpty(url);
        Assert.DoesNotContain(logger.Messages, message => message.Contains(url, StringComparison.Ordinal));
    }

    // ---- Scaffolding ----------------------------------------------------------------------

    private static Activity Activity(string type, string owner) => new()
    {
        Id = ActivityId,
        Title = "Ridge walk",
        Location = "North ridge",
        ActivityDate = new DateOnly(2026, 3, 14),
        Type = type,
        CreatedBy = owner,
    };

    private static Media Item(
        string id, string uploader, DateTimeOffset createdOn = default) => new()
        {
            Id = id,
            ActivityId = ActivityId,
            CreatedBy = uploader,
            Kind = Constant.MediaKind.Image,
            BlobPath = $"blobs/{id}",
            ContentType = "image/png",
            SizeBytes = 32,
            OriginalFileName = $"{id}.png",
            CreatedOn = createdOn,
        };

    /// <summary>One activity, one repository and one storage recorder, wired the way the app wires
    /// them.</summary>
    private sealed class Harness
    {
        private readonly string? _caller;
        private readonly SignedUrlLifetime _lifetime;

        public Harness(string? caller = Contributor, bool isAdmin = false, TimeSpan? mediaUrlTtl = null)
        {
            _caller = caller;
            _lifetime = new SignedUrlLifetime(mediaUrlTtl ?? SignedUrlLifetime.Default);

            Repository = new RecordingRepository
            {
                Activity = Activity(Constant.ActivityType.Public, Owner),

                // Only the caller's own row, and only when the caller holds the role: an id the
                // repository does not carry is a caller nobody promoted, which is the default.
                Users = caller is null || !isAdmin
                    ? []
                    : [new User { Id = caller, Role = Constant.UserRole.Admin }],
            };
            Storage = new RecordingStorage();

            Service = ServiceWith(Storage, NullLogger<MediaService>.Instance);
        }

        public RecordingRepository Repository { get; }

        public RecordingStorage Storage { get; }

        public MediaService Service { get; }

        /// <summary>The same wiring over a different storage double, for the tests whose storage is
        /// the thing being recorded and not the default recorder.</summary>
        public MediaService ServiceWith(IStorageRepository storage, ILogger<MediaService>? logger = null) =>
            new(
                Repository,
                storage,

                // The real rule, not a stand-in: the visibility and ownership decisions are the thing
                // under test in the gate tables, and a double here would be asserting the double.
                new ActivityAuthorizationService(Repository, new StubUserContext(_caller)),
                new UploadValidationService(),
                _lifetime,
                logger ?? NullLogger<MediaService>.Instance);
    }

    /// <summary>The caller's object id and nothing else.</summary>
    private sealed class StubUserContext(string? entraObjectId) : IUserContextService
    {
        public string? EntraObjectId { get; } = entraObjectId;

        public bool HasActiveRequest => true;

        public string? ActorName => null;

        public string? Email => null;

        public string? IpAddress => null;

        public string? UserAgent => null;

        public string? CorrelationId => null;
    }

    /// <summary>A repository that records what it was handed and stores nothing.</summary>
    private sealed class RecordingRepository : IDbRepository
    {
        public Activity? Activity { get; set; }

        public Media? Item { get; set; }

        public List<Media> Items { get; set; } = [];

        public List<User> Users { get; set; } = [];

        /// <summary>What the counted insert answers. False is an activity at its limit.</summary>
        public bool RoomForMore { get; set; } = true;

        public Media? Proposed { get; private set; }

        public List<Media> Inserted { get; } = [];

        public List<string> DeletedMedia { get; } = [];

        public int Cap { get; private set; }

        /// <summary>The predicate the counted insert was given, so a test can run it rather than
        /// read it.</summary>
        public Expression<Func<Media, bool>>? Counted { get; private set; }

        public Task<T?> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            if (typeof(T) == typeof(Activity))
            {
                return Task.FromResult((T?)(object?)Activity);
            }

            if (typeof(T) == typeof(Media))
            {
                return Task.FromResult((T?)(object?)Item);
            }

            // The predicate the service built is the one that runs, so a lookup that selected the
            // wrong row would fail here rather than reaching a row this double chose for it.
            if (typeof(T) == typeof(User))
            {
                var matches = (Func<User, bool>)(object)predicate.Compile();
                return Task.FromResult((T?)(object?)Users.FirstOrDefault(matches));
            }

            return Task.FromResult((T?)(object?)null);
        }

        public Task<List<T>> GetListAsync<T>(Expression<Func<T, bool>> predicate) where T : class =>
            Task.FromResult(
                typeof(T) == typeof(Media)
                    ? Items.Cast<T>().ToList()
                    : Users.Cast<T>().ToList());

        // The activity reads group media by activity; nothing here exercises that, so it answers
        // nothing rather than a number a test could lean on.
        public Task<Dictionary<string, int>> CountByAsync<T>(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, string>> key) where T : class =>
            throw new NotSupportedException(NoReads);

        public Task<(List<T> Items, int Total)> GetPageAsync<T>(
            Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
            int skip,
            int take) where T : class => throw new NotSupportedException(NoReads);

        public Task<int> CreateAsync<T>(T item) => throw new NotSupportedException(NoWrites);

        public Task<int> CreateAsync<T>(List<T> items) => throw new NotSupportedException(NoWrites);

        public Task<bool> CreateIfUnderAsync<T>(
            T item,
            Expression<Func<T, bool>> countOf,
            int cap) where T : class
        {
            Proposed = item as Media;
            Cap = cap;
            Counted = countOf as Expression<Func<Media, bool>>;

            if (RoomForMore)
            {
                Inserted.Add((Media)(object)item);
            }

            return Task.FromResult(RoomForMore);
        }

        public Task<int> DeleteAsync<T>(List<string> ids) where T : EntityBase
        {
            DeletedMedia.AddRange(ids);
            return Task.FromResult(ids.Count);
        }

        public Task<int> UpdateAsync<T>(T item) where T : EntityBase => throw new NotSupportedException(NoWrites);

        public Task<int> UpdateAsync<T>(List<T> items) where T : EntityBase =>
            throw new NotSupportedException(NoWrites);

        private const string NoReads = "This double answers no paged reads.";

        private const string NoWrites = "This double answers no writes but the inserted item.";
    }

    /// <summary>
    /// Records the asks the service makes of storage, and mints markers in their place.
    /// </summary>
    /// <remarks>
    /// It stands in for no storage behaviour: it holds no bytes and signs nothing, so it cannot show
    /// that a URL works — the container tier is where that is asked, because only a real backend can
    /// answer it. What it supports is the claim about the <em>ask</em>: which container, which path,
    /// and the instant the URL was given.
    /// </remarks>
    private sealed class RecordingStorage : IStorageRepository
    {
        public List<(string Container, string Path, DateTimeOffset ExpiresOn)> ReadUrls { get; } = [];

        public List<(string Container, string Path, string ContentType)> Uploads { get; } = [];

        public List<string> Deleted { get; } = [];

        public bool RefuseUploads { get; set; }

        public Task<string> UploadAsync(
            string container,
            string path,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            if (RefuseUploads)
            {
                throw new NotSupportedException(NoStorage);
            }

            Uploads.Add((container, path, contentType));
            return Task.FromResult(path);
        }

        public Task DeleteAsync(
            string container,
            string path,
            CancellationToken cancellationToken = default)
        {
            Deleted.Add(path);
            return Task.CompletedTask;
        }

        public Task<Uri> CreateReadUrlAsync(
            string container,
            string path,
            DateTimeOffset expiresOn,
            CancellationToken cancellationToken = default)
        {
            ReadUrls.Add((container, path, expiresOn));
            return Task.FromResult(new Uri($"https://signed.invalid/{container}/{path}"));
        }

        public Uri CreatePublicUrl(string container, string path) =>
            throw new NotSupportedException(NoStorage);

        public Task<string> MoveAsync(
            string sourceContainer,
            string sourcePath,
            string destinationContainer,
            string destinationPath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException(NoStorage);

        private const string NoStorage = "This double records calls and stores nothing.";
    }

    /// <summary>
    /// Records nothing but the fact that it was reached, and names the member it was reached through.
    /// </summary>
    /// <remarks>
    /// One pair of tests uses it, for one claim: that a refused request touches no storage at all.
    /// It stands in for no storage behaviour and exists because <see cref="RecordingStorage"/> can
    /// only report the members it knows about, while this answers for every one of them at once.
    /// </remarks>
    private sealed class NeverReachedStorage : IStorageRepository
    {
        public List<string> Reached { get; } = [];

        public Task<string> UploadAsync(
            string container,
            string path,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Mark(nameof(UploadAsync)).ToString());

        public Task DeleteAsync(
            string container,
            string path,
            CancellationToken cancellationToken = default)
        {
            Mark(nameof(DeleteAsync));
            return Task.CompletedTask;
        }

        public Task<Uri> CreateReadUrlAsync(
            string container,
            string path,
            DateTimeOffset expiresOn,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Mark(nameof(CreateReadUrlAsync)));

        public Uri CreatePublicUrl(string container, string path) => Mark(nameof(CreatePublicUrl));

        public Task<string> MoveAsync(
            string sourceContainer,
            string sourcePath,
            string destinationContainer,
            string destinationPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Mark(nameof(MoveAsync)).ToString());

        private Uri Mark(string member)
        {
            Reached.Add(member);
            return new Uri($"https://never.invalid/{member}");
        }
    }

    /// <summary>Keeps every message the service logged, so "the URL was not logged" is a count
    /// rather than a reading of the code.</summary>
    private sealed class RecordingLogger : ILogger<MediaService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
