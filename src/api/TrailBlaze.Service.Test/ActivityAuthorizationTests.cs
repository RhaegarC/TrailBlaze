namespace TrailBlaze.Service.Test;

using System.Linq.Expressions;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Model;
using TrailBlaze.Model.Authorization;
using TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// The permission rule on its own, driven as the matrix the PRD states it as.
/// </summary>
/// <remarks>
/// The two axes are independent and are read separately here: <c>Type</c> decides who may read an
/// entry, <c>CreatedBy</c> decides who may change one, and neither answers the other's question. The
/// cells that matter most are the ones where a <c>Private</c> row must be indistinguishable from an
/// absent one, and the two rows of the mutate matrix that would still pass under the intuitive but
/// wrong owner-only rule for media.
/// </remarks>
public sealed class ActivityAuthorizationTests
{
    private const string Owner = "the-owners-object-id";

    private const string Stranger = "a-signed-in-stranger";

    private const string Admin = "the-administrators-object-id";

    private const string AnonymousKind = "anonymous";

    private const string StrangerKind = "stranger";

    private const string OwnerKind = "owner";

    private const string AdminKind = "admin";

    private static readonly Caller AsStranger = new(Stranger, IsAdmin: false);

    private static readonly Caller AsOwner = new(Owner, IsAdmin: false);

    private static readonly Caller AsAdmin = new(Admin, IsAdmin: true);

    // ---- Reading ---------------------------------------------------------------------------

    [Theory]
    // Anonymous: the public entries, and only those. A Shared one is not merely refused, it is
    // not there.
    [InlineData(Constant.ActivityType.Public, AnonymousKind, true)]
    [InlineData(Constant.ActivityType.Shared, AnonymousKind, false)]
    [InlineData(Constant.ActivityType.Private, AnonymousKind, false)]
    // Signed in: those, plus the shared ones, plus whatever is the caller's own.
    [InlineData(Constant.ActivityType.Public, StrangerKind, true)]
    [InlineData(Constant.ActivityType.Shared, StrangerKind, true)]
    [InlineData(Constant.ActivityType.Private, StrangerKind, false)]
    [InlineData(Constant.ActivityType.Public, OwnerKind, true)]
    [InlineData(Constant.ActivityType.Shared, OwnerKind, true)]
    [InlineData(Constant.ActivityType.Private, OwnerKind, true)]
    // An administrator reads everything, including an entry its owner set to private.
    [InlineData(Constant.ActivityType.Public, AdminKind, true)]
    [InlineData(Constant.ActivityType.Shared, AdminKind, true)]
    [InlineData(Constant.ActivityType.Private, AdminKind, true)]
    public void The_read_rule_over_the_two_axes(string type, string callerKind, bool readable) =>
        Assert.Equal(readable, Rule.CanRead(Row(type, Owner), CallerOf(callerKind)));

    /// <summary>
    /// The paged query filters with the expression and the single-row routes with the compiled
    /// check, so a list and a detail read could answer differently about the same row. They are one
    /// rule, and this is what says so.
    /// </summary>
    [Theory]
    [InlineData(Constant.ActivityType.Public, AnonymousKind)]
    [InlineData(Constant.ActivityType.Shared, AnonymousKind)]
    [InlineData(Constant.ActivityType.Private, AnonymousKind)]
    [InlineData(Constant.ActivityType.Public, StrangerKind)]
    [InlineData(Constant.ActivityType.Shared, StrangerKind)]
    [InlineData(Constant.ActivityType.Private, StrangerKind)]
    [InlineData(Constant.ActivityType.Private, OwnerKind)]
    [InlineData(Constant.ActivityType.Private, AdminKind)]
    public void The_query_predicate_and_the_in_hand_check_agree(string type, string callerKind)
    {
        Activity entry = Row(type, Owner);
        Caller caller = CallerOf(callerKind);

        Assert.Equal(Rule.CanRead(entry, caller), Rule.VisibleTo(caller).Compile()(entry));
    }

    /// <summary>
    /// An id that names nothing is unreadable to everybody, including an administrator: the row is
    /// absent, which is not a question about privilege.
    /// </summary>
    [Fact]
    public void An_entry_that_names_nothing_is_unreadable_to_everyone()
    {
        Assert.False(Rule.CanRead(null, Caller.Anonymous));
        Assert.False(Rule.CanRead(null, AsOwner));
        Assert.False(Rule.CanRead(null, AsAdmin));
    }

    /// <summary>An id that names nothing cannot be changed either, by the same reasoning.</summary>
    [Fact]
    public void An_entry_that_names_nothing_is_unmutable_by_everyone()
    {
        Assert.False(Rule.CanMutate(null, Caller.Anonymous));
        Assert.False(Rule.CanMutate(null, AsOwner));
        Assert.False(Rule.CanMutate(null, AsAdmin));
    }

    // ---- Changing --------------------------------------------------------------------------

    [Theory]
    [InlineData(Constant.ActivityType.Public, AnonymousKind, false)]
    [InlineData(Constant.ActivityType.Private, AnonymousKind, false)]
    [InlineData(Constant.ActivityType.Public, StrangerKind, false)]
    [InlineData(Constant.ActivityType.Shared, StrangerKind, false)]
    [InlineData(Constant.ActivityType.Private, StrangerKind, false)]
    [InlineData(Constant.ActivityType.Public, OwnerKind, true)]
    [InlineData(Constant.ActivityType.Shared, OwnerKind, true)]
    [InlineData(Constant.ActivityType.Private, OwnerKind, true)]
    [InlineData(Constant.ActivityType.Public, AdminKind, true)]
    [InlineData(Constant.ActivityType.Private, AdminKind, true)]
    public void The_mutate_rule_follows_ownership_and_not_visibility(
        string type, string callerKind, bool mutable) =>
        Assert.Equal(mutable, Rule.CanMutate(Row(type, Owner), CallerOf(callerKind)));

    /// <summary>
    /// Readable but not changeable is the pair the callers of this rule have to keep apart, so the
    /// rule has to make both available for the same row rather than collapsing them.
    /// </summary>
    [Fact]
    public void An_entry_a_stranger_may_read_they_may_still_not_change()
    {
        Activity entry = Row(Constant.ActivityType.Public, Owner);

        Assert.True(Rule.CanRead(entry, AsStranger));
        Assert.False(Rule.CanMutate(entry, AsStranger));
    }

    // ---- Removing one item of media ---------------------------------------------------------

    /// <summary>
    /// Decision #27's three principals, written as an allow because the intuitive-but-wrong rule —
    /// that only the contributor may remove their own item — would pass every other test here.
    /// </summary>
    [Fact]
    public void The_uploader_the_entries_owner_and_an_administrator_may_remove_an_item()
    {
        Media item = Item(uploader: Stranger);
        Activity entry = Row(Constant.ActivityType.Public, Owner);

        Assert.True(Rule.CanRemoveMedia(item, entry, AsStranger));
        Assert.True(Rule.CanRemoveMedia(item, entry, AsOwner));
        Assert.True(Rule.CanRemoveMedia(item, entry, AsAdmin));
    }

    /// <summary>
    /// There are three principals and no fourth: a signed-in caller who is neither is refused, and
    /// the token alone buys nothing.
    /// </summary>
    [Fact]
    public void A_signed_in_caller_who_is_neither_is_refused()
    {
        Media item = Item(uploader: Stranger);
        Activity entry = Row(Constant.ActivityType.Public, Owner);

        Assert.False(Rule.CanRemoveMedia(item, entry, new Caller("neither-of-the-two", IsAdmin: false)));
    }

    [Fact]
    public void An_anonymous_caller_may_remove_nothing()
    {
        Media item = Item(uploader: Stranger);
        Activity entry = Row(Constant.ActivityType.Public, Owner);

        Assert.False(Rule.CanRemoveMedia(item, entry, Caller.Anonymous));
    }

    /// <summary>
    /// The activity may be gone — a media row whose entry no longer resolves — and the uploader is
    /// still the uploader. The owner half simply has nothing to match on.
    /// </summary>
    [Fact]
    public void An_uploader_may_still_remove_their_item_when_the_entry_does_not_resolve()
    {
        Media item = Item(uploader: Stranger);

        Assert.True(Rule.CanRemoveMedia(item, null, AsStranger));
        Assert.False(Rule.CanRemoveMedia(item, null, AsOwner));
    }

    // ---- Where the role comes from -----------------------------------------------------------

    /// <summary>
    /// Decision #9: the elevated role is read from the <c>users</c> row and from nothing else. The
    /// caller abstraction is the token's object id and no claim beyond it, so the row is the only
    /// thing left that can answer.
    /// </summary>
    [Fact]
    public async Task The_admin_determination_is_read_from_the_users_row()
    {
        var repository = new UserRows(
            UserRow(Admin, Constant.UserRole.Admin),
            UserRow(Stranger, Constant.UserRole.User));

        Assert.True((await Resolve(repository, Admin)).IsAdmin);
        Assert.False((await Resolve(repository, Stranger)).IsAdmin);
    }

    /// <summary>
    /// An allowlist, not a denylist: a value that merely looks administrative is not the
    /// administrator, so a column that grew a new role would grant nothing by accident.
    /// </summary>
    [Fact]
    public async Task A_role_that_is_not_the_admin_constant_is_not_the_admin()
    {
        Caller caller = await Resolve(new UserRows(UserRow(Owner, "Administrator")), Owner);

        Assert.False(caller.IsAdmin);
    }

    /// <summary>
    /// Auto-provisioning happens on first sight of an object id, so a signed-in caller with no row
    /// yet is an ordinary one rather than a fault.
    /// </summary>
    [Fact]
    public async Task A_signed_in_caller_with_no_row_is_an_ordinary_user()
    {
        Caller caller = await Resolve(new UserRows(), Stranger);

        Assert.Equal(Stranger, caller.Id);
        Assert.True(caller.IsSignedIn);
        Assert.False(caller.IsAdmin);
    }

    /// <summary>
    /// The public list is the one route an anonymous caller reaches in bulk, so a token-less request
    /// must not cost a query — and there is no row it could be about.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_request_naming_nobody_is_resolved_without_a_read(string? caller)
    {
        var repository = new UserRows(UserRow(Stranger, Constant.UserRole.Admin));

        Caller resolved = await Resolve(repository, caller);

        Assert.False(resolved.IsSignedIn);
        Assert.False(resolved.IsAdmin);
        Assert.Equal(0, repository.Reads);
    }

    /// <summary>
    /// An anonymous caller and a caller with a blank id are the same principal, and neither is
    /// distinguished by anything but the id: no claim widens <see cref="Caller.Anonymous"/>.
    /// </summary>
    [Fact]
    public void Anonymous_holds_no_id_and_no_privilege()
    {
        Assert.Null(Caller.Anonymous.Id);
        Assert.False(Caller.Anonymous.IsSignedIn);
        Assert.False(Caller.Anonymous.IsAdmin);
    }

    // ---- Scaffolding -------------------------------------------------------------------------

    /// <summary>One rule, built over a repository that holds no rows, for the decisions that need
    /// none: every read and every refusal above is decided from the entry and the caller alone.</summary>
    private static readonly ActivityAuthorizationService Rule = new(new UserRows(), new StubCaller(null));

    private static Task<Caller> Resolve(IDbRepository repository, string? caller) =>
        new ActivityAuthorizationService(repository, new StubCaller(caller)).ResolveAsync();

    private static Caller CallerOf(string kind) => kind switch
    {
        AnonymousKind => Caller.Anonymous,
        StrangerKind => AsStranger,
        OwnerKind => AsOwner,
        AdminKind => AsAdmin,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a caller this file names."),
    };

    private static Activity Row(string type, string owner) => new()
    {
        Id = "the-activity",
        Title = "Ridge walk",
        Location = "North ridge",
        ActivityDate = new DateOnly(2026, 3, 14),
        Type = type,
        CreatedBy = owner,
    };

    private static Media Item(string uploader) => new()
    {
        Id = "the-item",
        ActivityId = "the-activity",
        CreatedBy = uploader,
        Kind = Constant.MediaKind.Image,
        BlobPath = "the-activity/the-blob.png",
        ContentType = "image/png",
        SizeBytes = 32,
        OriginalFileName = "ridge.png",
    };

    private static User UserRow(string id, string role) => new() { Id = id, Role = role };

    /// <summary>The caller's object id and nothing else, which is all a token's claims come to
    /// here — there is no role among them for the service to prefer over the row.</summary>
    private sealed class StubCaller(string? entraObjectId) : IUserContextService
    {
        public string? EntraObjectId { get; } = entraObjectId;

        public bool HasActiveRequest => true;

        public string? ActorName => null;

        public string? Email => null;

        public string? IpAddress => null;

        public string? UserAgent => null;

        public string? CorrelationId => null;
    }

    /// <summary>A repository holding user rows and nothing else, which counts the reads it was
    /// asked for so "resolved without a query" can be asserted on the ask.</summary>
    private sealed class UserRows(params User[] rows) : IDbRepository
    {
        public int Reads { get; private set; }

        public Task<T?> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            Reads++;

            if (typeof(T) != typeof(User))
            {
                throw new NotSupportedException(NoReads);
            }

            var matches = (Func<User, bool>)(object)predicate.Compile();

            return Task.FromResult((T?)(object?)rows.FirstOrDefault(matches));
        }

        public Task<List<T>> GetListAsync<T>(Expression<Func<T, bool>> predicate) where T : class =>
            throw new NotSupportedException(NoReads);

        public Task<(List<T> Items, int Total)> GetPageAsync<T>(
            Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
            int skip,
            int take) where T : class => throw new NotSupportedException(NoReads);

        public Task<Dictionary<string, int>> CountByAsync<T>(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, string>> key) where T : class => throw new NotSupportedException(NoReads);

        public Task<int> CreateAsync<T>(T item) => throw new NotSupportedException(NoWrites);

        public Task<int> CreateAsync<T>(List<T> items) => throw new NotSupportedException(NoWrites);

        public Task<bool> CreateIfUnderAsync<T>(
            T item,
            Expression<Func<T, bool>> countOf,
            int cap) where T : class => throw new NotSupportedException(NoWrites);

        public Task<int> DeleteAsync<T>(List<string> ids) where T : EntityBase =>
            throw new NotSupportedException(NoWrites);

        public Task<int> UpdateAsync<T>(T item) where T : EntityBase => throw new NotSupportedException(NoWrites);

        public Task<int> UpdateAsync<T>(List<T> items) where T : EntityBase =>
            throw new NotSupportedException(NoWrites);

        private const string NoReads = "This double answers one read: a user row by id.";

        private const string NoWrites = "This double records no writes.";
    }
}
