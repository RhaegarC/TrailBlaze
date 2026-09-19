namespace TrailBlaze.Repository.Test;

using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// What a paged read of the activities table actually returns.
/// </summary>
/// <remarks>
/// The order and the filter are composed here rather than taken from the service, so what these
/// assert is the <em>repository's</em> half of a page: that the store applies the ordering, the
/// skip and the take, and that the count describes the filtered set. Which predicate the service
/// hands over is asserted offline in <c>TrailBlaze.Service.Test</c>.
/// </remarks>
[Trait("Category", "Container")]
public sealed class ActivityPagingTests(TrailBlazeDatabaseFixture fixture)
    : IClassFixture<TrailBlazeDatabaseFixture>
{
    private static readonly DateOnly RidgeDate = new(2026, 3, 14);

    [SkippableFact]
    public async Task A_page_comes_back_newest_first()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string owner = Owner();
        await SeedAsync(owner, "Public", "First", "Second", "Third");

        (List<Activity> items, _) = await PageAsync(owner, skip: 0, take: 10);

        Assert.Equal(3, items.Count);
        Assert.True(
            items.Zip(items.Skip(1)).All(pair => pair.First.CreatedOn >= pair.Second.CreatedOn),
            "The page is not in non-increasing creation order.");
    }

    [SkippableFact]
    public async Task Paging_returns_every_row_exactly_once()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string owner = Owner();
        await SeedAsync(owner, "Public", "First", "Second", "Third", "Fourth", "Fifth");

        (List<Activity> first, int total) = await PageAsync(owner, skip: 0, take: 2);
        (List<Activity> second, _) = await PageAsync(owner, skip: 2, take: 2);
        (List<Activity> third, _) = await PageAsync(owner, skip: 4, take: 2);

        Assert.Equal(5, total);
        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Single(third);

        string[] ids = [.. first.Concat(second).Concat(third).Select(row => row.Id)];
        Assert.Equal(5, ids.Distinct().Count());
    }

    [SkippableFact]
    public async Task A_page_past_the_end_is_empty()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string owner = Owner();
        await SeedAsync(owner, "Public", "First");

        (List<Activity> items, _) = await PageAsync(owner, skip: 20, take: 10);

        Assert.Empty(items);
    }

    [SkippableFact]
    public async Task A_deleted_row_leaves_the_page_and_the_count()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string owner = Owner();
        List<Activity> seeded = await SeedAsync(owner, "Public", "First", "Second");

        using (IServiceScope scope = fixture.CreateScope())
        {
            await new DatabaseRepository(scope.ServiceProvider.GetRequiredService<TrailBlazeContext>())
                .DeleteAsync<Activity>([seeded[0].Id]);
        }

        (List<Activity> items, int total) = await PageAsync(owner, skip: 0, take: 10);

        Assert.Equal(1, total);
        Assert.DoesNotContain(items, row => row.Id == seeded[0].Id);
    }

    /// <summary>
    /// A row the predicate excludes does not consume a page slot.
    /// </summary>
    /// <remarks>
    /// The visible rows are seeded <em>first</em> and the hidden ones after, so they are the
    /// older ones. Were the filter applied to a materialised page rather than in the query, the
    /// page would be filled by the newer hidden rows and then emptied by the filter, and this
    /// would read as an empty page with a total of two.
    /// </remarks>
    [SkippableFact]
    public async Task An_excluded_row_does_not_consume_a_page_slot()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string owner = Owner();
        await SeedAsync(owner, "Public", "Visible one", "Visible two");
        await SeedAsync(owner, "Private", "Hidden one", "Hidden two", "Hidden three");

        (List<Activity> items, int total) = await PageAsync(owner, skip: 0, take: 2);

        Assert.Equal(2, items.Count);
        Assert.Equal(2, total);
        Assert.All(items, row => Assert.Equal("Public", row.Type));
    }

    private async Task<(List<Activity> Items, int Total)> PageAsync(
        string owner, int skip, int take, string type = "Public")
    {
        using IServiceScope scope = fixture.CreateScope();
        var repository = new DatabaseRepository(
            scope.ServiceProvider.GetRequiredService<TrailBlazeContext>());

        return await repository.GetPageAsync<Activity>(
            row => row.CreatedByUserId == owner && row.Type == type,
            query => query.OrderByDescending(row => row.CreatedOn).ThenByDescending(row => row.Id),
            skip,
            take);
    }

    private async Task<List<Activity>> SeedAsync(string owner, string type, params string[] titles)
    {
        using IServiceScope scope = fixture.CreateScope();
        var repository = new DatabaseRepository(
            scope.ServiceProvider.GetRequiredService<TrailBlazeContext>());

        var rows = titles.Select(title => new Activity
        {
            Title = title,
            Location = "North ridge",
            ActivityDate = RidgeDate,
            Type = type,
            CreatedByUserId = owner,
        }).ToList();

        await repository.CreateAsync(rows);

        return rows;
    }

    /// <summary>
    /// A creator id no other test uses, so the page is scoped to the rows this test wrote: the
    /// database is shared, and nothing here may assert on the table as a whole.
    /// </summary>
    private static string Owner() => $"oid-{Guid.NewGuid():N}";
}
