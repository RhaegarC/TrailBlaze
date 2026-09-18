namespace TrailBlaze.Repository.Test;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The soft-delete filter executed: a row marked deleted disappears from ordinary queries and
/// stays in the table.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SoftDeleteFilterTests"/> proves the predicate is generated, by reading the SQL.
/// This proves it is <em>applied</em> by SQL Server, which is a different claim and the one a
/// user actually experiences. A predicate can be perfectly formed and still not be what the
/// engine executes — a translation that silently drops it, a filter EF applies in memory, or
/// a query that bypasses the model's filter entirely would all pass the offline test.
/// </para>
/// <para>
/// The pair matters in both directions: that the row vanishes from a normal query, and that
/// it is still <em>there</em> — a soft delete that removed the row would satisfy the first
/// assertion alone while being a hard delete.
/// </para>
/// </remarks>
[Trait("Category", "Container")]
public sealed class SoftDeleteExecutionTests(TrailBlazeDatabaseFixture fixture)
    : IClassFixture<TrailBlazeDatabaseFixture>
{
    [SkippableFact]
    public async Task A_soft_deleted_row_disappears_from_ordinary_queries_but_stays_in_the_table()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();
        var repository = new DatabaseRepository(context);

        var user = new User { DisplayName = "Ada" };
        await repository.CreateAsync(user);

        // Present before the delete, or the assertion below would pass for the wrong reason.
        Assert.NotNull(await repository.GetAsync<User>(candidate => candidate.Id == user.Id));

        // Two, not one. The count is SaveChanges' number of state entries written, and the
        // audit row the interceptor added is one of them — so this does not mean "rows the
        // caller named", which is what its name and its documentation suggest. Pinned as
        // observed rather than as intended: no caller consumes it today, so this is a
        // recorded oddity (see the debt register) rather than a regression to fix here.
        int changed = await repository.DeleteAsync<User>([user.Id]);
        Assert.Equal(2, changed);

        // The filter: gone from the query a reader would write.
        Assert.Null(await repository.GetAsync<User>(candidate => candidate.Id == user.Id));

        // Still there, flagged, for the one query that asks to see it. This is what separates
        // a soft delete from a delete — without it the test above would pass if the row had
        // been removed outright.
        User? withheld = await context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == user.Id);

        Assert.NotNull(withheld);
        Assert.True(withheld.IsDeleted);
        Assert.Equal("Ada", withheld.DisplayName);
    }

    /// <summary>
    /// A delete reaches its own row and no other, which is the failure a filter cannot catch:
    /// a predicate that is too broad hides the rows it wrongly matched just as convincingly as
    /// the one it was aimed at.
    /// </summary>
    [SkippableFact]
    public async Task A_soft_delete_leaves_the_rows_it_did_not_name_alone()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();
        var repository = new DatabaseRepository(context);

        var doomed = new User { DisplayName = "Ada" };
        var survivor = new User { DisplayName = "Grace" };
        await repository.CreateAsync<User>([doomed, survivor]);

        await repository.DeleteAsync<User>([doomed.Id]);

        Assert.Null(await repository.GetAsync<User>(candidate => candidate.Id == doomed.Id));
        Assert.NotNull(await repository.GetAsync<User>(candidate => candidate.Id == survivor.Id));
    }

    /// <summary>
    /// The soft delete is recorded in the history like any other write, and as a "Modified"
    /// rather than a "Deleted" — the row was not removed, so a history that said "Deleted"
    /// would describe an event that did not happen.
    /// </summary>
    [SkippableFact]
    public async Task A_soft_delete_is_recorded_as_a_modification()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();
        var repository = new DatabaseRepository(context);

        var user = new User { DisplayName = "Ada" };
        await repository.CreateAsync(user);
        await repository.DeleteAsync<User>([user.Id]);

        // A fresh scope, so the history comes from the database rather than the tracker.
        using IServiceScope reading = fixture.CreateScope();
        TrailBlazeContext reader = reading.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        List<AuditLog> history = await reader.AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityId == user.Id)
            .ToListAsync();

        Assert.Equal(2, history.Count);
        Assert.Contains(history, log => log.Action == "Added");
        Assert.Contains(history, log => log.Action == "Modified");
        Assert.DoesNotContain(history, log => log.Action == "Deleted");
    }
}
