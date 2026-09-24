namespace TrailBlaze.Repository.Test;

using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The two lookups an authorization decision is made from, executed against a real engine.
/// </summary>
/// <remarks>
/// The rule itself belongs to the Service tier and is driven there as a matrix; what this tier adds
/// is that the values the rule reads are the ones the row holds <em>now</em>. Both are read back
/// through a scope of their own, so the answer comes from the database rather than from the change
/// tracker that just wrote it. Nothing here restates the rule — each test asks the engine one
/// question about the column the rule compares.
/// </remarks>
[Trait("Category", "Container")]
public sealed class AuthorizationLookupTests(TrailBlazeDatabaseFixture fixture)
    : IClassFixture<TrailBlazeDatabaseFixture>
{
    private const string Owner = "oid-ada";
    private const string Editor = "oid-grace";

    private static readonly DateOnly RidgeDate = new(2026, 3, 14);

    /// <summary>
    /// The lookup the ownership decision reads — the row by id — answers with the row's owner.
    /// </summary>
    /// <remarks>
    /// Two entries by two owners in one test, because a lookup that ignored its predicate would
    /// return whichever row it found first and still satisfy a single-owner assertion.
    /// </remarks>
    [SkippableFact]
    public async Task The_lookup_the_ownership_decision_reads_answers_with_the_owner()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        Activity mine = await CreateAsync("Ridge walk", Owner);
        Activity theirs = await CreateAsync("Coast path", Editor);

        using IServiceScope scope = fixture.CreateScope();
        var repository = new DatabaseRepository(
            scope.ServiceProvider.GetRequiredService<TrailBlazeContext>());

        Activity? readMine = await repository.GetAsync<Activity>(row => row.Id == mine.Id);
        Activity? readTheirs = await repository.GetAsync<Activity>(row => row.Id == theirs.Id);

        Assert.Equal(Owner, readMine!.CreatedBy);
        Assert.Equal(Editor, readTheirs!.CreatedBy);
    }

    /// <summary>
    /// The owner the decision reads is the one the row was created with, not the caller who last
    /// edited it — the difference between ownership and the audit stamp beside it.
    /// </summary>
    [SkippableFact]
    public async Task The_owner_the_lookup_answers_with_survives_an_edit_by_someone_else()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        Activity written = await CreateAsync("Ridge walk", Owner);

        await RewriteAsync(written.Id, "Ridge walk, revised", Constant.ActivityType.Shared);

        using IServiceScope scope = fixture.CreateScope();
        var repository = new DatabaseRepository(
            scope.ServiceProvider.GetRequiredService<TrailBlazeContext>());

        Activity stored = (await repository.GetAsync<Activity>(row => row.Id == written.Id))!;

        Assert.Equal("Ridge walk, revised", stored.Title);
        Assert.Equal(Owner, stored.CreatedBy);

        // The save really was attributed to someone else, so the row above is evidence about
        // CreatedBy rather than about an edit that never happened.
        Assert.Equal(Editor, stored.LastModifiedBy);
    }

    /// <summary>
    /// The visibility lookup reads the type the row holds now: a change to <c>Type</c> moves the
    /// entry in and out of reach, and back, with each answer read from a scope that has written
    /// nothing.
    /// </summary>
    [SkippableFact]
    public async Task The_visibility_lookup_follows_a_change_to_the_type()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        Activity written = await CreateAsync("Ridge walk", Owner, Constant.ActivityType.Private);

        Assert.False(await ReachesPublicLookupAsync(written.Id));

        await RewriteAsync(written.Id, "Ridge walk", Constant.ActivityType.Public);
        Assert.True(await ReachesPublicLookupAsync(written.Id));

        await RewriteAsync(written.Id, "Ridge walk", Constant.ActivityType.Private);
        Assert.False(await ReachesPublicLookupAsync(written.Id));
    }

    /// <summary>
    /// An id that names no row answers null — the value a decision reads as absent, and the same
    /// answer a row the caller may not read is given, which is what keeps the two indistinguishable.
    /// </summary>
    [SkippableFact]
    public async Task A_lookup_for_an_id_that_names_nothing_answers_nothing()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        using IServiceScope scope = fixture.CreateScope();
        var repository = new DatabaseRepository(
            scope.ServiceProvider.GetRequiredService<TrailBlazeContext>());

        Assert.Null(await repository.GetAsync<Activity>(row => row.Id == Guid.NewGuid().ToString()));
    }

    /// <summary>
    /// Whether the narrowest visibility there is still matches, asked of the engine scoped to one
    /// id so the answer cannot come from another test's rows.
    /// </summary>
    private async Task<bool> ReachesPublicLookupAsync(string id)
    {
        using IServiceScope scope = fixture.CreateScope();
        var repository = new DatabaseRepository(
            scope.ServiceProvider.GetRequiredService<TrailBlazeContext>());

        return await repository.GetAsync<Activity>(
            row => row.Id == id && row.Type == Constant.ActivityType.Public) is not null;
    }

    private async Task<Activity> CreateAsync(string title, string owner, string? type = null)
    {
        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        Activity activity = Activity(title, owner, type);
        await new DatabaseRepository(context).CreateAsync(activity);

        return activity;
    }

    /// <summary>
    /// Rewrites the editable fields in a provider of its own, as <see cref="Editor"/> rather than
    /// as the owner — the caller a save is attributed to is the fixture's, so no other test can
    /// have this one's stamp.
    /// </summary>
    private async Task RewriteAsync(string id, string title, string type)
    {
        await using ServiceProvider provider = TestPersistence.Build(
            fixture.DatabaseConnectionString, FakeUserContext.Authenticated(Editor));

        using IServiceScope scope = provider.CreateScope();
        var repository = new DatabaseRepository(
            scope.ServiceProvider.GetRequiredService<TrailBlazeContext>());

        Activity stored = (await repository.GetAsync<Activity>(row => row.Id == id))!;
        stored.Title = title;
        stored.Type = type;

        await repository.UpdateAsync(stored);
    }

    private static Activity Activity(string title, string owner, string? type) => new()
    {
        Title = title,
        Location = "North ridge",
        ActivityDate = RidgeDate,
        Description = "A walk along the north ridge.",
        Type = type ?? Constant.ActivityType.Default,
        CreatedBy = owner,
    };
}
