namespace TrailBlaze.Repository.Test;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// An activity written and read back through a real SQL Server.
/// </summary>
/// <remarks>
/// The model tests say what the schema <em>is</em>; these say what happens to a row. A column
/// can be declared <c>date</c> in the model and created as something else by the migration, and
/// a round trip is the only thing that tells the two apart.
/// </remarks>
[Trait("Category", "Container")]
public sealed class ActivityRoundTripTests(TrailBlazeDatabaseFixture fixture)
    : IClassFixture<TrailBlazeDatabaseFixture>
{
    private static readonly DateOnly RidgeDate = new(2026, 3, 14);

    [SkippableFact]
    public async Task An_activity_survives_the_round_trip()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        Activity written = await CreateAsync("Ridge walk");

        // A fresh scope, so "read back" means read out of the database rather than seen in
        // the change tracker that wrote it.
        using IServiceScope reading = fixture.CreateScope();
        var reader = new DatabaseRepository(
            reading.ServiceProvider.GetRequiredService<TrailBlazeContext>());

        Activity stored = (await reader.GetAsync<Activity>(row => row.Id == written.Id))!;

        Assert.Equal("Ridge walk", stored.Title);
        Assert.Equal("North ridge", stored.Location);
        Assert.Equal(RidgeDate, stored.ActivityDate);
        Assert.Equal("A walk along the north ridge.", stored.Description);
        Assert.Equal("Public", stored.Type);
        Assert.Equal("oid-ada", stored.CreatedBy);
    }

    /// <summary>
    /// The migrated column is a <c>date</c> in the database, which the model test cannot say:
    /// it reads the model, and the model is not what a deployment creates.
    /// </summary>
    [SkippableFact]
    public async Task The_migrated_activity_date_column_is_a_bare_date()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        string? dataType = await ScalarAsync(
            context,
            "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS "
            + "WHERE TABLE_NAME = 'Activities' AND COLUMN_NAME = 'ActivityDate'");

        Assert.Equal("date", dataType);
    }

    /// <summary>
    /// The insert is stamped by the interceptor, and the stamp is in the row rather than only
    /// on the object that was saved.
    /// </summary>
    [SkippableFact]
    public async Task An_insert_stamps_the_audit_columns()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        Activity written = await CreateAsync("Ridge walk");
        Assert.NotEqual(default, written.CreatedOn);

        Assert.Equal(written.CreatedOn, await CreatedOnAsync(written.Id));
    }

    /// <summary>
    /// An edit leaves the entry's creation alone, because an entry does not become a different
    /// entry by being corrected.
    /// </summary>
    /// <remarks>
    /// <b>The update has to read the row first, and this is why.</b>
    /// <see cref="DatabaseRepository.UpdateAsync{T}"/> hands the whole entity to EF, which
    /// writes every property — including the audit columns the caller did not touch. An entity
    /// built from scratch for the update carries <c>default</c> there, so the row's
    /// <c>CreatedOn</c> becomes <c>0001-01-01</c> and the entry's place in the sort order with
    /// it. Nothing in the interceptor puts it back: the insert branch is the only one that
    /// stamps <c>CreatedOn</c>.
    /// </remarks>
    [SkippableFact]
    public async Task An_edit_keeps_the_creation_stamp_and_the_creator()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string id = (await CreateAsync("Ridge walk")).Id;
        DateTimeOffset created = await CreatedOnAsync(id);

        using (IServiceScope editing = fixture.CreateScope())
        {
            var repository = new DatabaseRepository(
                editing.ServiceProvider.GetRequiredService<TrailBlazeContext>());

            // Read, then change, then write — the shape the service uses.
            Activity stored = (await repository.GetAsync<Activity>(row => row.Id == id))!;
            stored.Title = "Ridge walk, revised";
            stored.Type = "Private";
            await repository.UpdateAsync(stored);
        }

        using IServiceScope reading = fixture.CreateScope();
        var reader = new DatabaseRepository(
            reading.ServiceProvider.GetRequiredService<TrailBlazeContext>());

        Activity revised = (await reader.GetAsync<Activity>(row => row.Id == id))!;

        Assert.Equal("Ridge walk, revised", revised.Title);
        Assert.Equal("Private", revised.Type);
        Assert.Equal(created, revised.CreatedOn);
        Assert.Equal("oid-ada", revised.CreatedBy);
    }

    /// <summary>
    /// The pair that separates a soft delete from a delete: gone from the read a caller
    /// performs, still a row in the table.
    /// </summary>
    [SkippableFact]
    public async Task A_deleted_activity_leaves_the_read_path_and_stays_in_the_table()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string id = (await CreateAsync("Ridge walk")).Id;

        using (IServiceScope deleting = fixture.CreateScope())
        {
            var repository = new DatabaseRepository(
                deleting.ServiceProvider.GetRequiredService<TrailBlazeContext>());

            await repository.DeleteAsync<Activity>([id]);
        }

        using IServiceScope reading = fixture.CreateScope();
        TrailBlazeContext context = reading.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        Assert.Null(await new DatabaseRepository(context).GetAsync<Activity>(row => row.Id == id));

        // Still there, flagged, for the one query that asks to see it. Without this the
        // assertion above would pass had the row been removed outright.
        Activity? withheld = await context.Activities
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id);

        Assert.NotNull(withheld);
        Assert.True(withheld.IsDeleted);
        Assert.Equal("Ridge walk", withheld.Title);
    }

    /// <summary>
    /// The engine refuses a type outside the closed set, which is the migration's claim and not
    /// the model's: the check constraint is SQL the migration emitted, and this is SQL Server
    /// rejecting a row with it.
    /// </summary>
    [SkippableFact]
    public async Task The_engine_refuses_a_type_outside_the_closed_set()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();
        var repository = new DatabaseRepository(context);

        var rogue = Activity("Ridge walk");
        rogue.Type = "Friends";

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.CreateAsync(rogue));
    }

    private async Task<Activity> CreateAsync(string title)
    {
        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        Activity activity = Activity(title);
        await new DatabaseRepository(context).CreateAsync(activity);

        return activity;
    }

    private static Activity Activity(string title) => new()
    {
        Title = title,
        Location = "North ridge",
        ActivityDate = RidgeDate,
        Description = "A walk along the north ridge.",
        Type = "Public",
        CreatedBy = "oid-ada",
    };

    /// <summary>The creation stamp as the row holds it, through a scope of its own.</summary>
    private async Task<DateTimeOffset> CreatedOnAsync(string id)
    {
        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        return await context.Activities
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.Id == id)
            .Select(row => row.CreatedOn)
            .SingleAsync();
    }

    /// <summary>A single value from one statement, over the context's own connection, so the
    /// answer comes from the engine rather than from EF's view of the model.</summary>
    private static async Task<string?> ScalarAsync(TrailBlazeContext context, string sql)
    {
        DbConnection connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        return (await command.ExecuteScalarAsync())?.ToString();
    }
}
