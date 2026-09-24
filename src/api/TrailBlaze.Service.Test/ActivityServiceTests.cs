namespace TrailBlaze.Service.Test;

using Microsoft.Extensions.Logging.Abstractions;
using System.Linq.Expressions;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Model;
using TrailBlaze.Model.Activity;
using TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// The activity rules, and what the service hands a store.
/// </summary>
public sealed class ActivityServiceTests
{
    private const string Caller = "the-callers-object-id";

    private const string SomebodyElse = "another-users-object-id";

    private static readonly DateOnly Date = new(2026, 3, 14);

    private static readonly DateTimeOffset Early = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Late = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    // ---- What is stored -------------------------------------------------------------------

    [Fact]
    public async Task A_created_activity_is_attributed_to_the_caller()
    {
        var repository = new RecordingRepository();

        ActivityOutcome outcome = await Service(repository).CreateAsync(Valid());

        Assert.Equal(ActivityOutcomeKind.Completed, outcome.Kind);
        Assert.Equal(Caller, repository.Created!.CreatedBy);
    }

    [Fact]
    public async Task A_created_activity_is_stored_with_the_canonical_type()
    {
        var repository = new RecordingRepository();

        await Service(repository).CreateAsync(Valid() with { Type = "shared" });

        Assert.Equal("Shared", repository.Created!.Type);
    }

    [Fact]
    public async Task A_created_activity_carries_its_own_generated_key()
    {
        var repository = new RecordingRepository();

        await Service(repository).CreateAsync(Valid());

        Assert.False(string.IsNullOrWhiteSpace(repository.Created!.Id));
    }

    [Fact]
    public async Task A_refused_activity_reaches_the_repository_not_at_all()
    {
        var repository = new RecordingRepository();

        ActivityOutcome outcome = await Service(repository).CreateAsync(Valid() with { Title = "  " });

        Assert.Equal(ActivityOutcomeKind.Rejected, outcome.Kind);
        Assert.Null(repository.Created);
    }

    [Fact]
    public async Task A_request_naming_no_caller_creates_nothing()
    {
        var repository = new RecordingRepository();

        ActivityOutcome outcome = await Service(repository, caller: null).CreateAsync(Valid());

        Assert.Equal(ActivityOutcomeKind.NoCaller, outcome.Kind);
        Assert.Null(repository.Created);
    }

    // ---- Field rules ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_title_that_is_not_there_is_refused(string? title) =>
        Assert.Contains(TitleField, Service().Validate(Valid() with { Title = title }).Keys);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t ")]
    public void A_location_that_is_not_there_is_refused(string? location) =>
        Assert.Contains(LocationField, Service().Validate(Valid() with { Location = location }).Keys);

    [Fact]
    public void A_title_at_the_cap_is_accepted() =>
        Assert.Empty(Service().Validate(Valid() with { Title = Filled(Constant.ActivityField.TitleLength) }));

    [Fact]
    public void A_title_one_character_over_the_cap_is_refused() =>
        Assert.Contains(
            TitleField,
            Service().Validate(Valid() with { Title = Filled(Constant.ActivityField.TitleLength + 1) }).Keys);

    [Fact]
    public void A_location_at_the_cap_is_accepted() =>
        Assert.Empty(Service().Validate(Valid() with { Location = Filled(Constant.ActivityField.LocationLength) }));

    [Fact]
    public void A_location_one_character_over_the_cap_is_refused() =>
        Assert.Contains(
            LocationField,
            Service().Validate(Valid() with { Location = Filled(Constant.ActivityField.LocationLength + 1) }).Keys);

    [Fact]
    public void A_missing_activity_date_is_refused() =>
        Assert.Contains(DateField, Service().Validate(Valid() with { ActivityDate = null }).Keys);

    [Fact]
    public void Every_bad_field_is_reported_at_once()
    {
        IReadOnlyDictionary<string, string[]> errors =
            Service().Validate(new CreateActivityRequest { Title = " ", Location = " ", Type = "Friends" });

        Assert.Contains(TitleField, errors.Keys);
        Assert.Contains(LocationField, errors.Keys);
        Assert.Contains(DateField, errors.Keys);
        Assert.Contains(TypeField, errors.Keys);
    }

    [Theory]
    [InlineData("Public")]
    [InlineData("public")]
    [InlineData("SHARED")]
    [InlineData("Private")]
    public void A_type_from_the_closed_set_is_accepted(string type) =>
        Assert.Empty(Service().Validate(Valid() with { Type = type }));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_absent_type_is_accepted(string? type) =>
        Assert.Empty(Service().Validate(Valid() with { Type = type }));

    [Theory]
    [InlineData("Friends")]
    [InlineData("Publicc")]
    [InlineData("private entry")]
    public void A_type_outside_the_closed_set_is_refused(string type) =>
        Assert.Contains(TypeField, Service().Validate(Valid() with { Type = type }).Keys);

    [Fact]
    public void A_padded_type_is_still_the_type_it_names() =>
        Assert.Empty(Service().Validate(Valid() with { Type = "  shared  " }));

    [Fact]
    public void An_absent_description_is_stored_as_null()
    {
        Assert.Null(ActivityDraft.From(Valid() with { Description = "   " }).Description);
        Assert.Null(ActivityDraft.From(Valid() with { Description = null }).Description);
    }

    [Fact]
    public void The_surrounding_whitespace_is_not_stored()
    {
        ActivityDraft draft = ActivityDraft.From(Valid() with { Title = "  Ridge walk  ", Location = " North  " });

        Assert.Equal("Ridge walk", draft.Title);
        Assert.Equal("North", draft.Location);
    }

    [Fact]
    public void The_stored_type_is_the_canonical_one()
    {
        Assert.Equal(Constant.ActivityType.Public, ActivityDraft.From(Valid() with { Type = "public" }).Type);
        Assert.Equal(Constant.ActivityType.Shared, ActivityDraft.From(Valid() with { Type = "  shared " }).Type);
        Assert.Equal(Constant.ActivityType.Private, ActivityDraft.From(Valid() with { Type = "PRIVATE" }).Type);
    }

    [Fact]
    public void An_absent_type_is_stored_as_the_default() =>
        Assert.Equal(Constant.ActivityType.Default, ActivityDraft.From(Valid() with { Type = null }).Type);

    // ---- Paging ---------------------------------------------------------------------------

    [Fact]
    public async Task The_first_page_is_ten_rows_long_and_starts_at_the_first_row()
    {
        var repository = new RecordingRepository();

        ActivityPage page = await Service(repository).GetPageAsync(page: 0, pageSize: 0);

        Assert.Equal(0, repository.Skip);
        Assert.Equal(Constant.ActivityPaging.DefaultPageSize, repository.Take);
        Assert.Equal(0, page.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public async Task A_page_size_of_zero_or_less_falls_back_to_the_default(int requested)
    {
        var repository = new RecordingRepository();

        await Service(repository).GetPageAsync(page: 0, pageSize: requested);

        Assert.Equal(Constant.ActivityPaging.DefaultPageSize, repository.Take);
    }

    [Fact]
    public async Task A_page_size_above_the_cap_is_clamped_rather_than_honoured()
    {
        var repository = new RecordingRepository();

        ActivityPage page = await Service(repository).GetPageAsync(page: 0, pageSize: 10_000);

        Assert.Equal(Constant.ActivityPaging.MaxPageSize, repository.Take);
        Assert.Equal(Constant.ActivityPaging.MaxPageSize, page.PageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task A_page_size_within_the_accepted_range_is_honoured(int requested)
    {
        var repository = new RecordingRepository();

        await Service(repository).GetPageAsync(page: 0, pageSize: requested);

        Assert.Equal(requested, repository.Take);
    }

    [Fact]
    public async Task A_negative_page_index_is_read_as_the_first_page()
    {
        var repository = new RecordingRepository();

        ActivityPage page = await Service(repository).GetPageAsync(page: -3, pageSize: 10);

        Assert.Equal(0, repository.Skip);
        Assert.Equal(0, page.Page);
    }

    [Fact]
    public async Task A_later_page_skips_the_rows_the_earlier_ones_returned()
    {
        var repository = new RecordingRepository();

        await Service(repository).GetPageAsync(page: 2, pageSize: 10);

        Assert.Equal(20, repository.Skip);
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_rather_than_an_error()
    {
        var repository = new RecordingRepository();

        ActivityPage page = await Service(repository).GetPageAsync(page: int.MaxValue, pageSize: 100);

        // Saturating the skip is the point: an overflowing one would reach the store as a
        // negative OFFSET, which is a failed query rather than an empty page.
        Assert.Equal(int.MaxValue, repository.Skip);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task A_page_reports_the_size_of_the_filtered_set()
    {
        var repository = new RecordingRepository { PageTotal = 137 };

        ActivityPage page = await Service(repository).GetPageAsync(page: 0, pageSize: 10);

        Assert.Equal(137, page.Total);
    }

    // ---- Visibility -----------------------------------------------------------------------

    [Fact]
    public async Task An_anonymous_page_reaches_public_entries_only()
    {
        var repository = new RecordingRepository();

        await Service(repository, caller: null).GetPageAsync(page: 0, pageSize: 10);

        Func<Activity, bool> visible = repository.PagePredicate!.Compile();

        Assert.True(visible(Row(Constant.ActivityType.Public)));
        Assert.False(visible(Row(Constant.ActivityType.Shared)));
        Assert.False(visible(Row(Constant.ActivityType.Private)));
    }

    [Fact]
    public async Task A_signed_in_page_reaches_public_shared_and_its_own_private_entries()
    {
        var repository = new RecordingRepository();

        await Service(repository).GetPageAsync(page: 0, pageSize: 10);

        Func<Activity, bool> visible = repository.PagePredicate!.Compile();

        Assert.True(visible(Row(Constant.ActivityType.Public, owner: SomebodyElse)));
        Assert.True(visible(Row(Constant.ActivityType.Shared, owner: SomebodyElse)));
        Assert.True(visible(Row(Constant.ActivityType.Private, owner: Caller)));
        Assert.False(visible(Row(Constant.ActivityType.Private, owner: SomebodyElse)));
    }

    /// <summary>
    /// A Public activity's cover lives in the world-readable container, so the anonymous list can
    /// render it — and it is handed over unsigned, because a SAS on a public blob grants nothing
    /// the container had not already granted while turning a permanent link into one that dies.
    /// </summary>
    [Fact]
    public async Task A_public_entrys_cover_reaches_an_anonymous_caller_unsigned()
    {
        var repository = new RecordingRepository
        {
            PageItems = [Row(Constant.ActivityType.Public, coverPath: "covers/ridge.jpg")],
        };
        var storage = new RecordingStorage();

        ActivityPage page = await Service(repository, storage, caller: null).GetPageAsync(page: 0, pageSize: 10);

        Assert.NotNull(page.Items[0].CoverImageUrl);
        Assert.Equal(Constant.StorageContainer.Covers, Assert.Single(storage.PublicUrls).Container);
        Assert.Empty(storage.ReadUrls);
    }

    /// <summary>
    /// A Shared or Private cover lives in the private container, so the URL is signed and expiring
    /// — and it is the only way the bytes are reachable at all.
    /// </summary>
    [Fact]
    public async Task A_shared_entrys_cover_reaches_a_signed_in_caller_signed()
    {
        var repository = new RecordingRepository
        {
            PageItems = [Row(Constant.ActivityType.Shared, coverPath: "media/cover.jpg")],
        };
        var storage = new RecordingStorage();

        ActivityPage page = await Service(repository, storage).GetPageAsync(page: 0, pageSize: 10);

        Assert.NotNull(page.Items[0].CoverImageUrl);
        Assert.Equal(Constant.StorageContainer.Media, Assert.Single(storage.ReadUrls).Container);
        Assert.Empty(storage.PublicUrls);
    }

    // ---- Ordering -------------------------------------------------------------------------

    [Fact]
    public async Task A_page_is_ordered_by_creation_newest_first()
    {
        var repository = new RecordingRepository();

        await Service(repository).GetPageAsync(page: 0, pageSize: 10);

        Activity older = Row(Constant.ActivityType.Public, createdOn: Early);
        Activity newer = Row(Constant.ActivityType.Public, createdOn: Late);

        Assert.Equal([newer.Id, older.Id], Ordered(repository, older, newer));
    }

    [Fact]
    public async Task Rows_created_at_the_same_moment_are_ordered_by_id_so_pages_do_not_overlap()
    {
        var repository = new RecordingRepository();

        await Service(repository).GetPageAsync(page: 0, pageSize: 10);

        Activity first = Row(Constant.ActivityType.Public, createdOn: Early, id: "aaa");
        Activity second = Row(Constant.ActivityType.Public, createdOn: Early, id: "bbb");

        Assert.Equal([second.Id, first.Id], Ordered(repository, first, second));
    }

    // ---- Reading one activity -------------------------------------------------------------

    [Theory]
    [InlineData(Constant.ActivityType.Public, null)]
    [InlineData(Constant.ActivityType.Public, Caller)]
    [InlineData(Constant.ActivityType.Shared, Caller)]
    [InlineData(Constant.ActivityType.Private, Caller)]
    public async Task An_entry_the_caller_may_read_is_returned(string type, string? caller)
    {
        var repository = new RecordingRepository { Existing = Row(type, owner: Caller) };

        ActivityOutcome outcome = await Service(repository, caller: caller).GetAsync("the-activity");

        Assert.Equal(ActivityOutcomeKind.Completed, outcome.Kind);
        Assert.NotNull(outcome.Activity);
    }

    /// <summary>
    /// Not-found rather than forbidden, and carrying no row. An "exists but you may not read it"
    /// answer would confirm the id names something, which is the fact being withheld.
    /// </summary>
    [Theory]
    [InlineData(Constant.ActivityType.Shared, null)]
    [InlineData(Constant.ActivityType.Private, SomebodyElse)]
    public async Task An_entry_the_caller_may_not_read_is_answered_as_absent(string type, string? caller)
    {
        var repository = new RecordingRepository { Existing = Row(type, owner: Caller) };

        ActivityOutcome outcome = await Service(repository, caller: caller).GetAsync("the-activity");

        Assert.Equal(ActivityOutcomeKind.NotFound, outcome.Kind);
        Assert.Null(outcome.Activity);
    }

    [Fact]
    public async Task An_id_that_names_no_activity_is_answered_as_absent()
    {
        var repository = new RecordingRepository();

        ActivityOutcome outcome = await Service(repository).GetAsync("the-activity");

        Assert.Equal(ActivityOutcomeKind.NotFound, outcome.Kind);
        Assert.Null(outcome.Activity);
    }

    /// <summary>
    /// The count is a read of the media table, so it is gated by the entry's own rule rather than
    /// being a disclosure of its own: a caller who may not read the entry never causes the count to
    /// be taken. Asserted on the read not happening, which is stronger than a null field.
    /// </summary>
    [Fact]
    public async Task An_entry_the_caller_may_not_read_is_never_counted()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Private, owner: Caller),
        };

        await Service(repository, caller: SomebodyElse).GetAsync("the-activity");

        Assert.False(repository.CountedMedia);
    }

    [Fact]
    public async Task An_entry_the_caller_may_read_is_counted()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, id: "the-activity"),
            MediaCounts = new() { ["the-activity"] = 2 },
        };

        ActivityOutcome outcome = await Service(repository).GetAsync("the-activity");

        Assert.Equal(2, outcome.Activity!.MediaCount);
    }

    // ---- The creator, and the cover URL ---------------------------------------------------

    [Fact]
    public async Task A_read_names_the_creator_by_display_name()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, owner: SomebodyElse),
            Users = [new User { Id = SomebodyElse, DisplayName = "Ada" }],
        };

        ActivityOutcome outcome = await Service(repository).GetAsync("the-activity");

        Assert.Equal("Ada", outcome.Activity!.CreatorDisplayName);
    }

    /// <summary>The id is the client's signal for whether to offer edit controls, so it is the one
    /// identity field a signed-in caller gets and an anonymous one does not.</summary>
    [Fact]
    public async Task An_anonymous_read_of_a_public_entry_carries_no_user_id()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, owner: SomebodyElse),
        };

        ActivityOutcome outcome = await Service(repository, caller: null).GetAsync("the-activity");

        Assert.Equal(ActivityOutcomeKind.Completed, outcome.Kind);
        Assert.Null(outcome.Activity!.CreatedByUserId);
    }

    [Fact]
    public async Task A_signed_in_read_carries_the_creators_id()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, owner: SomebodyElse),
        };

        ActivityOutcome outcome = await Service(repository).GetAsync("the-activity");

        Assert.Equal(SomebodyElse, outcome.Activity!.CreatedByUserId);
    }

    /// <summary>
    /// The container is the whole of the public/private answer, so the choice of container *is* the
    /// choice between an unsigned URL and a signed one.
    /// </summary>
    [Theory]
    [InlineData(Constant.ActivityType.Shared)]
    [InlineData(Constant.ActivityType.Private)]
    public async Task A_shared_or_private_entrys_cover_is_signed_and_short_lived(string type)
    {
        var repository = new RecordingRepository
        {
            Existing = Row(type, owner: Caller, coverPath: "media/cover.jpg"),
        };
        var storage = new RecordingStorage();

        await Service(repository, storage).GetAsync("the-activity");

        (string container, string path, TimeSpan lifetime) = Assert.Single(storage.ReadUrls);
        Assert.Equal(Constant.StorageContainer.Media, container);
        Assert.Equal("media/cover.jpg", path);

        // The expiry is the real control on a bearer link, so it is bounded rather than merely
        // positive — a lifetime of a year would satisfy "expiring" and leak the bytes.
        Assert.InRange(lifetime, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));
        Assert.Empty(storage.PublicUrls);
    }

    [Fact]
    public async Task An_entry_with_no_cover_mints_no_url()
    {
        var repository = new RecordingRepository { Existing = Row(Constant.ActivityType.Public) };
        var storage = new RecordingStorage();

        ActivityOutcome outcome = await Service(repository, storage).GetAsync("the-activity");

        Assert.Equal(ActivityOutcomeKind.Completed, outcome.Kind);
        Assert.Null(outcome.Activity!.CoverImageUrl);
        Assert.Empty(storage.PublicUrls);
        Assert.Empty(storage.ReadUrls);
    }

    // ---- Deleting an activity -------------------------------------------------------------

    /// <summary>
    /// The delete is soft, so the entry can be restored — and its media with it. Removing the items
    /// here would bring a restored activity back with none of its pictures.
    /// </summary>
    [Fact]
    public async Task Deleting_an_activity_leaves_its_media_alone()
    {
        var repository = new RecordingRepository { Existing = Row(Constant.ActivityType.Public) };

        ActivityOutcome outcome = await Service(repository).DeleteAsync("the-activity");

        Assert.Equal(ActivityOutcomeKind.Deleted, outcome.Kind);
        Assert.Equal(["the-activity"], repository.DeletedActivities);
        Assert.Empty(repository.DeletedMedia);
    }

    [Fact]
    public async Task An_id_that_names_no_activity_deletes_nothing()
    {
        var repository = new RecordingRepository();

        ActivityOutcome outcome = await Service(repository).DeleteAsync("the-activity");

        Assert.Equal(ActivityOutcomeKind.NotFound, outcome.Kind);
        Assert.Empty(repository.DeletedActivities);
        Assert.Empty(repository.DeletedMedia);
    }

    // ---- Uploading a cover ----------------------------------------------------------------

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    [InlineData("IMAGE/JPEG")]
    public async Task An_image_on_the_allowlist_is_stored(string contentType)
    {
        var repository = new RecordingRepository { Existing = Row(Constant.ActivityType.Public) };
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(repository, storage)
            .UploadCoverAsync("the-activity", Bytes(1024), contentType, 1024);

        Assert.Equal(CoverOutcomeKind.Uploaded, outcome.Kind);
        Assert.Equal(contentType, Assert.Single(storage.Uploads).ContentType);
    }

    /// <summary>
    /// Refused by the shared image rule rather than by a rule of this route's own, and asserted as
    /// "no blob was written" rather than only as the outcome: a rejection that had already stored
    /// the bytes would leave an object nothing points at, and would pass an assertion made on the
    /// answer alone.
    /// </summary>
    [Theory]
    [InlineData("video/mp4")]
    [InlineData("video/quicktime")]
    [InlineData("application/pdf")]
    [InlineData("image/bmp")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_file_that_is_not_an_allowed_image_is_refused_and_writes_no_blob(
        string? contentType)
    {
        var repository = new RecordingRepository { Existing = Row(Constant.ActivityType.Public) };
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(repository, storage)
            .UploadCoverAsync("the-activity", Bytes(1024), contentType, 1024);

        Assert.Equal(CoverOutcomeKind.Rejected, outcome.Kind);
        Assert.Contains(CoverField, outcome.Errors!.Keys);
        Assert.Empty(storage.Uploads);
        Assert.Empty(repository.Saved);
    }

    /// <summary>
    /// The cap is inclusive, and the byte over it is refused by the shared image rule — the same
    /// pair the media route is held to, because it is the same 10 MB.
    /// </summary>
    [Fact]
    public async Task An_image_exactly_at_the_cap_is_accepted()
    {
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(
                new RecordingRepository { Existing = Row(Constant.ActivityType.Public) }, storage)
            .UploadCoverAsync(
                "the-activity", Bytes(Constant.Upload.ImageSizeCapBytes),
                "image/jpeg", Constant.Upload.ImageSizeCapBytes);

        Assert.Equal(CoverOutcomeKind.Uploaded, outcome.Kind);
        Assert.Single(storage.Uploads);
    }

    [Fact]
    public async Task An_image_one_byte_over_the_cap_is_refused_and_writes_no_blob()
    {
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(
                new RecordingRepository { Existing = Row(Constant.ActivityType.Public) }, storage)
            .UploadCoverAsync(
                "the-activity", Bytes(Constant.Upload.ImageSizeCapBytes + 1),
                "image/jpeg", Constant.Upload.ImageSizeCapBytes + 1);

        Assert.Equal(CoverOutcomeKind.Rejected, outcome.Kind);
        Assert.Empty(storage.Uploads);
    }

    /// <summary>
    /// The routing rule the whole feature rests on (Decision #29), asserted per type rather than
    /// once: a single happy-path check passes just as well when the branch is inverted for the
    /// other two.
    /// </summary>
    [Theory]
    [InlineData(Constant.ActivityType.Public, Constant.StorageContainer.Covers)]
    [InlineData(Constant.ActivityType.Shared, Constant.StorageContainer.Media)]
    [InlineData(Constant.ActivityType.Private, Constant.StorageContainer.Media)]
    public async Task A_cover_lands_in_the_container_its_entrys_type_requires(
        string type, string expected)
    {
        var storage = new RecordingStorage();

        await Service(new RecordingRepository { Existing = Row(type) }, storage)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Assert.Equal(expected, Assert.Single(storage.Uploads).Container);
    }

    /// <summary>
    /// One field either way: the client is handed a URL that works and never learns which container
    /// holds the bytes. The unsigned half is not cosmetic — a SAS on a public blob grants nothing
    /// the container had not already granted while turning a permanent link into one that dies.
    /// </summary>
    [Theory]
    [InlineData(Constant.ActivityType.Public, true)]
    [InlineData(Constant.ActivityType.Shared, false)]
    [InlineData(Constant.ActivityType.Private, false)]
    public async Task A_public_entrys_cover_comes_back_unsigned_and_a_private_ones_signed(
        string type, bool unsigned)
    {
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(
                new RecordingRepository { Existing = Row(type) }, storage)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Assert.NotNull(outcome.Cover!.CoverImageUrl);

        if (unsigned)
        {
            Assert.Equal(Constant.StorageContainer.Covers, Assert.Single(storage.PublicUrls).Container);
            Assert.Empty(storage.ReadUrls);
        }
        else
        {
            Assert.Equal(Constant.StorageContainer.Media, Assert.Single(storage.ReadUrls).Container);
            Assert.Empty(storage.PublicUrls);
        }
    }

    [Fact]
    public async Task An_upload_points_the_activity_at_the_stored_path()
    {
        var repository = new RecordingRepository { Existing = Row(Constant.ActivityType.Public) };
        var storage = new RecordingStorage();

        await Service(repository, storage)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        string stored = Assert.Single(storage.Uploads).Path;

        Assert.Equal(stored, Assert.Single(repository.Saved).CoverImageBlobPath);
    }

    /// <summary>
    /// A cover upload is one column's write. Asserted field by field, because
    /// <c>UpdateAsync</c> writes every property and a body that defaulted one would blank it.
    /// </summary>
    [Fact]
    public async Task An_upload_changes_nothing_but_the_cover_path()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, coverPath: null),
        };

        await Service(repository, new RecordingStorage())
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Activity saved = Assert.Single(repository.Saved);

        Assert.Equal("Ridge walk", saved.Title);
        Assert.Equal("North ridge", saved.Location);
        Assert.Equal(Date, saved.ActivityDate);
        Assert.Equal(Constant.ActivityType.Public, saved.Type);
        Assert.Equal(Caller, saved.CreatedBy);
    }

    /// <summary>
    /// Replace is the only mutation this feature offers, so an orphaned blob would accumulate with
    /// every edit. Asserted on the old path being deleted as well as on the new one being stored.
    /// </summary>
    [Fact]
    public async Task A_replacement_deletes_the_previous_blob()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, coverPath: "the-activity/the-old-one.jpg"),
        };
        var storage = new RecordingStorage();

        await Service(repository, storage)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        string stored = Assert.Single(storage.Uploads).Path;
        (string container, string path) = Assert.Single(storage.Deletes);

        Assert.Equal(Constant.StorageContainer.Covers, container);
        Assert.Equal("the-activity/the-old-one.jpg", path);
        Assert.NotEqual(path, stored);
    }

    /// <summary>
    /// The first cover deletes nothing: a delete of a path that was never stored would be a call
    /// the tier has no reason to make, and on a real backend it would be indistinguishable from a
    /// correct one.
    /// </summary>
    [Fact]
    public async Task A_first_cover_deletes_nothing()
    {
        var storage = new RecordingStorage();

        await Service(new RecordingRepository { Existing = Row(Constant.ActivityType.Public) }, storage)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Assert.Empty(storage.Deletes);
    }

    /// <summary>
    /// A refused replacement leaves the previous cover standing — the entry keeps the image it had
    /// rather than losing it to a bad upload.
    /// </summary>
    [Fact]
    public async Task A_refused_replacement_leaves_the_existing_cover_alone()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, coverPath: "the-activity/the-old-one.jpg"),
        };
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(repository, storage)
            .UploadCoverAsync("the-activity", Bytes(1024), "video/mp4", 1024);

        Assert.Equal(CoverOutcomeKind.Rejected, outcome.Kind);
        Assert.Empty(storage.Uploads);
        Assert.Empty(storage.Deletes);
        Assert.Empty(repository.Saved);
        Assert.Equal("the-activity/the-old-one.jpg", repository.Existing!.CoverImageBlobPath);
    }

    [Fact]
    public async Task An_upload_against_an_unknown_activity_writes_nothing()
    {
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(new RecordingRepository(), storage)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Assert.Equal(CoverOutcomeKind.NotFound, outcome.Kind);
        Assert.Empty(storage.Uploads);
    }

    /// <summary>
    /// Not-found rather than forbidden, matching the read rule: confirming the id names something
    /// is the fact being withheld. And nothing is stored — an upload that wrote the bytes before
    /// the gate would put an image in the account on behalf of a caller who may not see the entry.
    /// </summary>
    /// <remarks>
    /// A signed-in caller who is not the owner, because an anonymous one is turned away earlier by
    /// the missing-caller branch and would never reach this gate — and because a <c>Private</c>
    /// entry is the only one the read rule withholds from a caller who has a token: any signed-in
    /// caller may read a <c>Shared</c> one.
    /// </remarks>
    [Fact]
    public async Task An_upload_the_caller_may_not_read_writes_nothing()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Private, owner: SomebodyElse),
        };
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(repository, storage, Caller)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Assert.Equal(CoverOutcomeKind.NotFound, outcome.Kind);
        Assert.Empty(storage.Uploads);
        Assert.Empty(repository.Saved);
    }

    /// <summary>
    /// Before the entry is read, whatever the entry's visibility: a caller the request cannot name
    /// is refused without the store being asked anything.
    /// </summary>
    [Theory]
    [InlineData(Constant.ActivityType.Public)]
    [InlineData(Constant.ActivityType.Private)]
    public async Task An_upload_naming_no_caller_writes_nothing(string type)
    {
        var repository = new RecordingRepository { Existing = Row(type) };
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(repository, storage, caller: null)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Assert.Equal(CoverOutcomeKind.NoCaller, outcome.Kind);
        Assert.Empty(storage.Uploads);
        Assert.Empty(repository.Saved);
    }

    /// <summary>
    /// A cover shares the private container's blobs with media but never its rows: the two are
    /// separate concerns that happen to sit side by side (Decision #14). Asserted on the writes the
    /// service made, so a cover that also inserted or removed an item would fail here.
    /// </summary>
    [Fact]
    public async Task A_cover_upload_touches_no_media_row()
    {
        var repository = new RecordingRepository { Existing = Row(Constant.ActivityType.Private) };
        var storage = new RecordingStorage();

        await Service(repository, storage)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Assert.Equal(Constant.StorageContainer.Media, Assert.Single(storage.Uploads).Container);
        Assert.Single(repository.Saved);
        Assert.Empty(repository.DeletedMedia);
        Assert.Null(repository.Created);
    }

    /// <summary>
    /// The client is handed a URL and nothing else, which is what keeps the two containers from
    /// leaking into a payload. Asserted as an exact set, so a field added later fails here.
    /// </summary>
    [Fact]
    public void A_cover_response_carries_the_url_and_nothing_else() =>
        Assert.Equal(
            ["CoverImageUrl"],
            typeof(CoverResponse).GetProperties().Select(property => property.Name).Order());

    // ---- Who may change an entry ----------------------------------------------------------
    //
    // Ownership, not visibility, and the two answers are not interchangeable: a caller who may read
    // the entry is told they may not change it (403), and one who may not read it is told nothing at
    // all (404). Both are asserted on one route so the pair cannot silently collapse into one.

    [Theory]
    [InlineData(Constant.ActivityType.Public, ActivityOutcomeKind.Forbidden)]
    [InlineData(Constant.ActivityType.Shared, ActivityOutcomeKind.Forbidden)]
    [InlineData(Constant.ActivityType.Private, ActivityOutcomeKind.NotFound)]
    public async Task Editing_another_users_entry_is_told_the_one_thing_that_is_true(
        string type, ActivityOutcomeKind expected)
    {
        var repository = new RecordingRepository { Existing = Row(type, owner: SomebodyElse) };

        ActivityOutcome outcome = await Service(repository, caller: Caller)
            .UpdateAsync("the-activity", Update());

        Assert.Equal(expected, outcome.Kind);
        Assert.Empty(repository.Saved);
    }

    /// <summary>
    /// Refused before the body is read, for the reason the id check is first: a caller with no right
    /// to edit is not owed a field-by-field answer about an edit they were never going to make.
    /// </summary>
    [Fact]
    public async Task A_non_owner_is_refused_before_the_body_is_validated()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, owner: SomebodyElse),
        };

        ActivityOutcome outcome = await Service(repository, caller: Caller)
            .UpdateAsync("the-activity", new UpdateActivityRequest());

        Assert.Equal(ActivityOutcomeKind.Forbidden, outcome.Kind);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task The_owner_may_edit_their_own_entry()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Private, owner: Caller),
        };

        ActivityOutcome outcome = await Service(repository, caller: Caller)
            .UpdateAsync("the-activity", Update());

        Assert.Equal(ActivityOutcomeKind.Completed, outcome.Kind);
        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task An_administrator_may_edit_an_entry_they_do_not_own()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, owner: SomebodyElse),
            Users = [new User { Id = Caller, Role = Constant.UserRole.Admin }],
        };

        ActivityOutcome outcome = await Service(repository, caller: Caller)
            .UpdateAsync("the-activity", Update());

        Assert.Equal(ActivityOutcomeKind.Completed, outcome.Kind);
        Assert.Single(repository.Saved);
    }

    [Theory]
    [InlineData(Constant.ActivityType.Public, ActivityOutcomeKind.Forbidden)]
    [InlineData(Constant.ActivityType.Private, ActivityOutcomeKind.NotFound)]
    public async Task Deleting_another_users_entry_is_told_the_one_thing_that_is_true(
        string type, ActivityOutcomeKind expected)
    {
        var repository = new RecordingRepository { Existing = Row(type, owner: SomebodyElse) };

        ActivityOutcome outcome = await Service(repository, caller: Caller).DeleteAsync("the-activity");

        Assert.Equal(expected, outcome.Kind);
        Assert.Empty(repository.DeletedActivities);
    }

    [Fact]
    public async Task An_administrator_may_delete_an_entry_they_do_not_own()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Private, owner: SomebodyElse),
            Users = [new User { Id = Caller, Role = Constant.UserRole.Admin }],
        };

        ActivityOutcome outcome = await Service(repository, caller: Caller).DeleteAsync("the-activity");

        Assert.Equal(ActivityOutcomeKind.Deleted, outcome.Kind);
        Assert.Equal(["the-activity"], repository.DeletedActivities);
    }

    /// <summary>
    /// A cover is the entry's own face rather than a contribution to it, so here the rule is
    /// ownership and a stranger's read access is not a licence to replace it — the opposite of the
    /// media route, and the crossing this feature is most likely to get wrong in either direction.
    /// </summary>
    [Theory]
    [InlineData(Constant.ActivityType.Public, CoverOutcomeKind.Forbidden)]
    [InlineData(Constant.ActivityType.Private, CoverOutcomeKind.NotFound)]
    public async Task A_non_owner_cannot_give_another_users_entry_a_face(
        string type, CoverOutcomeKind expected)
    {
        var repository = new RecordingRepository { Existing = Row(type, owner: SomebodyElse) };
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(repository, storage, Caller)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Assert.Equal(expected, outcome.Kind);
        Assert.Empty(storage.Uploads);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task The_owner_may_give_their_own_entry_a_face()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, owner: Caller),
        };
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(repository, storage, Caller)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Assert.Equal(CoverOutcomeKind.Uploaded, outcome.Kind);
        Assert.Single(storage.Uploads);
    }

    [Fact]
    public async Task An_administrator_may_give_any_entry_a_face()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, owner: SomebodyElse),
            Users = [new User { Id = Caller, Role = Constant.UserRole.Admin }],
        };
        var storage = new RecordingStorage();

        CoverOutcome outcome = await Service(repository, storage, Caller)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Assert.Equal(CoverOutcomeKind.Uploaded, outcome.Kind);
        Assert.Single(storage.Uploads);
    }

    /// <summary>
    /// The ordering claim: a refusal reaches storage at all — through any member, not only the ones
    /// a recording double happens to list.
    /// </summary>
    /// <remarks>
    /// The double is purpose-built for this one test and stands in for nothing: it names the member
    /// it was reached through and returns nothing meaningful, so what it supports is the absence of a
    /// call rather than the behaviour of a store. That absence cannot be asked of a real backend,
    /// which is silent about not having been called.
    /// </remarks>
    [Fact]
    public async Task A_refused_change_reaches_storage_not_at_all()
    {
        var storage = new NeverReachedStorage();
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, owner: SomebodyElse),
        };

        CoverOutcome outcome = await Service(repository, storage, Caller)
            .UploadCoverAsync("the-activity", Bytes(1024), "image/jpeg", 1024);

        Assert.Empty(storage.Reached);
        Assert.Equal(CoverOutcomeKind.Forbidden, outcome.Kind);
    }

    // ---- The visibility change moves the cover --------------------------------------------

    /// <summary>
    /// A change across the public line relocates the bytes, and this is the criterion that makes
    /// the rule worth having: a public URL cannot be recalled, so a cover left in the public
    /// container by a `Public` → `Private` edit stays fetchable by anyone who ever held the link.
    /// The destination container is the pair that matters, so it is the pair asserted.
    /// </summary>
    [Theory]
    [InlineData(Constant.ActivityType.Public, Constant.ActivityType.Private,
        Constant.StorageContainer.Covers, Constant.StorageContainer.Media)]
    [InlineData(Constant.ActivityType.Public, Constant.ActivityType.Shared,
        Constant.StorageContainer.Covers, Constant.StorageContainer.Media)]
    [InlineData(Constant.ActivityType.Private, Constant.ActivityType.Public,
        Constant.StorageContainer.Media, Constant.StorageContainer.Covers)]
    [InlineData(Constant.ActivityType.Shared, Constant.ActivityType.Public,
        Constant.StorageContainer.Media, Constant.StorageContainer.Covers)]
    public async Task A_change_across_the_public_line_moves_the_cover(
        string from, string to, string expectedSource, string expectedDestination)
    {
        var repository = new RecordingRepository
        {
            Existing = Row(from, coverPath: "the-activity/the-cover.jpg"),
        };
        var storage = new RecordingStorage();

        await Service(repository, storage).UpdateAsync("the-activity", Update(to));

        (string source, string sourcePath, string destination, string destinationPath) =
            Assert.Single(storage.Moves);

        Assert.Equal(expectedSource, source);
        Assert.Equal(expectedDestination, destination);
        Assert.Equal("the-activity/the-cover.jpg", sourcePath);
        Assert.Equal("the-activity/the-cover.jpg", destinationPath);
    }

    /// <summary>
    /// The move is followed by the row keeping the destination the storage reported, rather than
    /// the path the service passed in: the two happen to be equal today, and a caller that assumed
    /// so would store a path nothing is stored at the moment the contract chooses otherwise.
    /// </summary>
    [Fact]
    public async Task The_moved_path_is_what_the_activity_keeps()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, coverPath: "the-activity/the-cover.jpg"),
        };

        await Service(repository, new RecordingStorage())
            .UpdateAsync("the-activity", Update(Constant.ActivityType.Private));

        Assert.Equal("the-activity/the-cover.jpg", Assert.Single(repository.Saved).CoverImageBlobPath);
    }

    /// <summary>
    /// Both containers are private, so nothing about the entry's audience changes and there is
    /// nothing to relocate.
    /// </summary>
    [Theory]
    [InlineData(Constant.ActivityType.Shared, Constant.ActivityType.Private)]
    [InlineData(Constant.ActivityType.Private, Constant.ActivityType.Shared)]
    public async Task A_change_between_the_two_private_types_moves_nothing(
        string from, string to)
    {
        var repository = new RecordingRepository
        {
            Existing = Row(from, coverPath: "the-activity/the-cover.jpg"),
        };
        var storage = new RecordingStorage();

        await Service(repository, storage).UpdateAsync("the-activity", Update(to));

        Assert.Empty(storage.Moves);
        Assert.Equal("the-activity/the-cover.jpg", Assert.Single(repository.Saved).CoverImageBlobPath);
    }

    [Fact]
    public async Task A_type_change_on_an_entry_with_no_cover_moves_nothing()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, coverPath: null),
        };
        var storage = new RecordingStorage();

        await Service(repository, storage)
            .UpdateAsync("the-activity", Update(Constant.ActivityType.Private));

        Assert.Empty(storage.Moves);
        Assert.Null(Assert.Single(repository.Saved).CoverImageBlobPath);
    }

    /// <summary>
    /// An edit that does not name a type leaves the entry's visibility — and so its cover's
    /// container — exactly where it was.
    /// </summary>
    [Fact]
    public async Task An_edit_that_does_not_name_a_type_moves_nothing()
    {
        var repository = new RecordingRepository
        {
            Existing = Row(Constant.ActivityType.Public, coverPath: "the-activity/the-cover.jpg"),
        };
        var storage = new RecordingStorage();

        await Service(repository, storage).UpdateAsync("the-activity", Update(type: null));

        Assert.Empty(storage.Moves);
        Assert.Equal(Constant.ActivityType.Public, Assert.Single(repository.Saved).Type);
    }

    // ---- Scaffolding ----------------------------------------------------------------------

    private const string TitleField = nameof(CreateActivityRequest.Title);

    private const string LocationField = nameof(CreateActivityRequest.Location);

    private const string DateField = nameof(CreateActivityRequest.ActivityDate);

    private const string TypeField = nameof(CreateActivityRequest.Type);

    /// <summary>The form field a refused cover reports its reason under — the name the route reads,
    /// so a client can put the message beside the control that caused it.</summary>
    private const string CoverField = "file";

    private static ActivityService Service(
        IDbRepository? repository = null,
        IStorageRepository? storage = null,
        string? caller = Caller)
    {
        // One repository for both, so a role a test put on the rows is the role the rule reads.
        var rows = repository ?? new RecordingRepository();

        return new(
            rows,
            storage ?? new RecordingStorage(),
            new UploadValidationService(),
            new ActivityAuthorizationService(rows, new StubUserContext(caller)),
            new StubUserContext(caller),
            NullLogger<ActivityService>.Instance);
    }

    private static CreateActivityRequest Valid() => new()
    {
        Title = "Ridge walk",
        Location = "North ridge",
        ActivityDate = Date,
    };

    /// <summary>A full update body, so the only field under test is the type.</summary>
    private static UpdateActivityRequest Update(string? type = null) => new()
    {
        Title = "Ridge walk",
        Location = "North ridge",
        ActivityDate = Date,
        Type = type,
    };

    /// <summary>A stream of exactly this many bytes, which is all the size rule reads.</summary>
    private static MemoryStream Bytes(long length) => new(new byte[length]);

    private static Activity Row(
        string type,
        string? owner = null,
        DateTimeOffset createdOn = default,
        string? id = null,
        string? coverPath = null) => new()
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Title = "Ridge walk",
            Location = "North ridge",
            ActivityDate = Date,
            Type = type,
            CreatedBy = owner ?? Caller,
            CreatedOn = createdOn,
            CoverImageBlobPath = coverPath,
        };

    private static string[] Ordered(RecordingRepository repository, params Activity[] rows) =>
        [.. repository.OrderBy!(rows.AsQueryable()).Select(row => row.Id)];

    private static string Filled(int length) => new('a', length);

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
        public Activity? Created { get; private set; }

        public Expression<Func<Activity, bool>>? PagePredicate { get; private set; }

        public Func<IQueryable<Activity>, IOrderedQueryable<Activity>>? OrderBy { get; private set; }

        public int Skip { get; private set; }

        public int Take { get; private set; }

        public List<Activity> PageItems { get; set; } = [];

        public int PageTotal { get; set; }

        /// <summary>The row the delete path loads, or null for an id that names nothing.</summary>
        public Activity? Existing { get; set; }

        public List<string> DeletedActivities { get; } = [];

        public List<string> DeletedMedia { get; } = [];

        /// <summary>The <c>users</c> rows the name join resolves against.</summary>
        public List<User> Users { get; set; } = [];

        /// <summary>How many media rows each activity id carries, as a grouped count would answer.</summary>
        public Dictionary<string, int> MediaCounts { get; set; } = new();

        /// <summary>Whether the media table was counted at all — the read a caller who may not see
        /// the entry must never cause.</summary>
        public bool CountedMedia { get; private set; }

        /// <summary>The rows handed to a save, in the order they arrived.</summary>
        public List<Activity> Saved { get; } = [];

        public Task<int> CreateAsync<T>(T item)
        {
            Created = item as Activity;
            return Task.FromResult(1);
        }

        public Task<(List<T> Items, int Total)> GetPageAsync<T>(
            Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
            int skip,
            int take) where T : class
        {
            PagePredicate = predicate as Expression<Func<Activity, bool>>;
            OrderBy = orderBy as Func<IQueryable<Activity>, IOrderedQueryable<Activity>>;
            Skip = skip;
            Take = take;

            return Task.FromResult((PageItems.Cast<T>().ToList(), PageTotal));
        }

        // Answered only for the two reads the service performs, so the rest of the contract stays
        // refused and no test can lean on a read this double does not model.
        public Task<T?> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            if (typeof(T) == typeof(Activity))
            {
                return Task.FromResult((T?)(object?)Existing);
            }

            // The predicate the service built is the one that runs, so a lookup that selected the
            // wrong row fails here rather than reaching a row this double chose for it.
            if (typeof(T) == typeof(User))
            {
                var matches = (Func<User, bool>)(object)predicate.Compile();
                return Task.FromResult((T?)(object?)Users.FirstOrDefault(matches));
            }

            throw new NotSupportedException(NoReads);
        }

        public Task<List<T>> GetListAsync<T>(Expression<Func<T, bool>> predicate) where T : class =>
            typeof(T) == typeof(User)
                ? Task.FromResult(Users.Cast<T>().ToList())
                : throw new NotSupportedException(NoReads);

        // Records the ask as well as answering it, so a test can assert the count was never taken
        // rather than only that the field came back null.
        public Task<Dictionary<string, int>> CountByAsync<T>(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, string>> key) where T : class
        {
            CountedMedia = true;

            return Task.FromResult(
                typeof(T) == typeof(Media)
                    ? new Dictionary<string, int>(MediaCounts)
                    : throw new NotSupportedException(NoReads));
        }

        public Task<int> CreateAsync<T>(List<T> items) => throw new NotSupportedException(NoWrites);

        // Records by type, so media the delete path should not be touching shows up in an assertion
        // rather than being absorbed into the activity's own list.
        public Task<int> DeleteAsync<T>(List<string> ids) where T : EntityBase
        {
            if (typeof(T) == typeof(Media))
            {
                DeletedMedia.AddRange(ids);
            }
            else
            {
                DeletedActivities.AddRange(ids);
            }

            return Task.FromResult(ids.Count);
        }

        public Task<bool> CreateIfUnderAsync<T>(
            T item,
            Expression<Func<T, bool>> countOf,
            int cap) where T : class => throw new NotSupportedException(NoWrites);

        // Records the row as well as accepting it, so a test can read what the service wrote — the
        // cover path a replace stored, or the fields an upload must have left alone.
        public Task<int> UpdateAsync<T>(T item) where T : EntityBase
        {
            Saved.Add((Activity)(object)item!);
            return Task.FromResult(1);
        }

        public Task<int> UpdateAsync<T>(List<T> items) where T : EntityBase =>
            throw new NotSupportedException(NoWrites);

        private const string NoReads = "This double answers no reads.";

        private const string NoWrites = "This double answers no writes.";
    }

    /// <summary>
    /// Records the asks the service makes of storage, and mints markers in their place.
    /// </summary>
    /// <remarks>
    /// It stands in for no storage behaviour: it holds no bytes and signs nothing, so it cannot
    /// show that a URL works, that a blob moved, or that a deleted one is unreachable — the
    /// container tier is where each of those is asked, because only a real backend can answer them.
    /// What it supports is the claim about the <em>ask</em>, which a real backend is silent about:
    /// which container a cover was sent to, whether its URL was signed or handed over plain, what a
    /// replacement deleted, and which two containers a visibility change moved between.
    /// </remarks>
    private sealed class RecordingStorage : IStorageRepository
    {
        public List<(string Container, string Path)> PublicUrls { get; } = [];

        public List<(string Container, string Path, TimeSpan Lifetime)> ReadUrls { get; } = [];

        public List<(string Container, string Path, string ContentType)> Uploads { get; } = [];

        public List<(string Container, string Path)> Deletes { get; } = [];

        public List<(string Source, string SourcePath, string Destination, string DestinationPath)>
            Moves { get; } = [];

        public Task<Uri> CreateReadUrlAsync(
            string container,
            string path,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default)
        {
            ReadUrls.Add((container, path, lifetime));
            return Task.FromResult(new Uri($"https://signed.invalid/{container}/{path}"));
        }

        public Uri CreatePublicUrl(string container, string path)
        {
            PublicUrls.Add((container, path));
            return new Uri($"https://public.invalid/{container}/{path}");
        }

        public Task<string> UploadAsync(
            string container,
            string path,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            Uploads.Add((container, path, contentType));
            return Task.FromResult(path);
        }

        public Task DeleteAsync(
            string container,
            string path,
            CancellationToken cancellationToken = default)
        {
            Deletes.Add((container, path));
            return Task.CompletedTask;
        }

        public Task<string> MoveAsync(
            string sourceContainer,
            string sourcePath,
            string destinationContainer,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            Moves.Add((sourceContainer, sourcePath, destinationContainer, destinationPath));
            return Task.FromResult(destinationPath);
        }
    }

    /// <summary>
    /// Records nothing but the fact that it was reached, and names the member it was reached
    /// through.
    /// </summary>
    /// <remarks>
    /// One test uses it, for one claim: that a refused request touches no storage. It stands in for
    /// no storage behaviour — it holds no bytes, signs nothing and moves nothing — and it exists
    /// because <see cref="RecordingStorage"/> can only report the members it knows about, while this
    /// answers for every one of them at once.
    /// </remarks>
    private sealed class NeverReachedStorage : IStorageRepository
    {
        public List<string> Reached { get; } = [];

        public Task<Uri> CreateReadUrlAsync(
            string container,
            string path,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Mark(nameof(CreateReadUrlAsync)));

        public Uri CreatePublicUrl(string container, string path) =>
            Mark(nameof(CreatePublicUrl));

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
            return new Uri("https://never-reached.invalid/");
        }
    }
}
