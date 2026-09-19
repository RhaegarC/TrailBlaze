namespace TrailBlaze.Service.Test;

using System.Linq.Expressions;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Model.Activity;
using TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// What the service hands to the repository, and what it hands over when the input is refused.
/// </summary>
/// <remarks>
/// These are the claims about the service that can be made without a store — attribution, and
/// the refusal that writes nothing. What the database then does with the row is
/// <c>TrailBlaze.Repository.Test</c>'s subject.
/// </remarks>
public sealed class ActivityServiceTests
{
    private const string Caller = "the-callers-object-id";

    private static readonly DateOnly Date = new(2026, 3, 14);

    [Fact]
    public async Task A_created_activity_is_attributed_to_the_caller()
    {
        var repository = new RecordingRepository();

        ActivityOutcome outcome = await Service(repository).CreateAsync(Valid());

        Assert.Equal(ActivityOutcomeKind.Completed, outcome.Kind);
        Assert.Equal(Caller, repository.Created!.CreatedByUserId);
    }

    /// <summary>A caller's id cannot arrive in the body — <see cref="CreateActivityRequest"/> has
    /// no property for one — and this is the other half: the id that is stored is the one read
    /// from the request in flight.</summary>
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

    private static ActivityService Service(IDbRepository repository, string? caller = Caller) =>
        new(repository, new StubUserContext(caller), new ActivityValidationService());

    private static CreateActivityRequest Valid() => new()
    {
        Title = "Ridge walk",
        Location = "North ridge",
        ActivityDate = Date,
    };

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

    /// <summary>
    /// A repository that records what it was handed and stores nothing.
    /// </summary>
    /// <remarks>
    /// It stands in for no database behaviour: every read throws, and the one write it answers
    /// records its argument and reports a row. It exists for the two claims a service test can
    /// make about a store — what was handed over, and that nothing was. It is not the deleted
    /// in-memory fake in a new costume: that implemented the contract it was asserting, so a
    /// round trip through it proved only that a dictionary tolerates a key. Nothing here can be
    /// read back, which is exactly what keeps it from pretending to be a database.
    /// </remarks>
    private sealed class RecordingRepository : IDbRepository
    {
        public Activity? Created { get; private set; }

        public Task<int> CreateAsync<T>(T item)
        {
            Created = item as Activity;
            return Task.FromResult(1);
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
