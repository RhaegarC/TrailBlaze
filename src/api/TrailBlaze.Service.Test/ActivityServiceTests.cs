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
        Assert.Equal(Caller, repository.Created!.CreatedByUserId);
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

    [Fact]
    public async Task An_anonymous_page_carries_no_cover_path()
    {
        var repository = new RecordingRepository
        {
            PageItems = [Row(Constant.ActivityType.Public, coverPath: "media/cover.jpg")],
        };

        ActivityPage page = await Service(repository, caller: null).GetPageAsync(page: 0, pageSize: 10);

        Assert.Null(page.Items[0].CoverImageBlobPath);
    }

    [Fact]
    public async Task A_signed_in_page_carries_the_cover_path()
    {
        var repository = new RecordingRepository
        {
            PageItems = [Row(Constant.ActivityType.Public, coverPath: "media/cover.jpg")],
        };

        ActivityPage page = await Service(repository).GetPageAsync(page: 0, pageSize: 10);

        Assert.Equal("media/cover.jpg", page.Items[0].CoverImageBlobPath);
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

    // ---- Scaffolding ----------------------------------------------------------------------

    private const string TitleField = nameof(CreateActivityRequest.Title);

    private const string LocationField = nameof(CreateActivityRequest.Location);

    private const string DateField = nameof(CreateActivityRequest.ActivityDate);

    private const string TypeField = nameof(CreateActivityRequest.Type);

    private static ActivityService Service(IDbRepository? repository = null, string? caller = Caller) =>
        new(repository ?? new RecordingRepository(), new StubUserContext(caller), NullLogger<ActivityService>.Instance);

    private static CreateActivityRequest Valid() => new()
    {
        Title = "Ridge walk",
        Location = "North ridge",
        ActivityDate = Date,
    };

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
            CreatedByUserId = owner ?? Caller,
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

        public Task<T?> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : class =>
            throw new NotSupportedException(NoReads);

        public Task<List<T>> GetListAsync<T>(Expression<Func<T, bool>> predicate) where T : class =>
            throw new NotSupportedException(NoReads);

        public Task<int> CreateAsync<T>(List<T> items) => throw new NotSupportedException(NoWrites);

        public Task<int> DeleteAsync<T>(List<string> ids) where T : EntityBase =>
            throw new NotSupportedException(NoWrites);

        public Task<int> UpdateAsync<T>(T item) where T : EntityBase => throw new NotSupportedException(NoWrites);

        public Task<int> UpdateAsync<T>(List<T> items) where T : EntityBase =>
            throw new NotSupportedException(NoWrites);

        private const string NoReads = "This double answers no reads.";

        private const string NoWrites = "This double answers no writes.";
    }
}
