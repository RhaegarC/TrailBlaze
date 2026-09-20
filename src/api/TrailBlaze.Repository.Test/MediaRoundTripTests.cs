namespace TrailBlaze.Repository.Test;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// An item of media written and read back through a real SQL Server, and the counted insert that
/// enforces the per-contributor cap.
/// </summary>
/// <remarks>
/// <see cref="MediaModelTests"/> says what the schema <em>is</em>; these say what happens to a row.
/// The cap in particular cannot be asserted anywhere else: it is a count and an insert that have to
/// be one step against the engine, and no double can show whether they are.
/// </remarks>
[Trait("Category", "Container")]
public sealed class MediaRoundTripTests(TrailBlazeDatabaseFixture fixture)
    : IClassFixture<TrailBlazeDatabaseFixture>
{
    /// <summary>A size no <c>int</c> column could hold, so a narrowed column fails here.</summary>
    private const long VideoSize = Constant.Upload.VideoSizeCapBytes;

    /// <summary>The uploader every cap test counts, and the one the seeded rows belong to.</summary>
    private const string Ada = "oid-ada";

    /// <summary>A second contributor on the same activity, who the cap must not count against Ada.</summary>
    private const string Grace = "oid-grace";

    [SkippableFact]
    public async Task An_item_survives_the_round_trip()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string activityId = Guid.NewGuid().ToString();
        var written = Item(activityId, "ridge.png", Constant.MediaKind.Image, 4_096);

        using (IServiceScope scope = fixture.CreateScope())
        {
            await Repository(scope).CreateAsync(written);
        }

        // A fresh scope, so "read back" means read out of the database rather than seen in the
        // change tracker that wrote it.
        using IServiceScope reading = fixture.CreateScope();

        Media stored = (await Repository(reading)
            .GetAsync<Media>(row => row.Id == written.Id))!;

        Assert.Equal(activityId, stored.ActivityId);
        Assert.Equal("oid-ada", stored.CreatedBy);
        Assert.Equal(Constant.MediaKind.Image, stored.Kind);
        Assert.Equal("image/png", stored.ContentType);
        Assert.Equal(4_096, stored.SizeBytes);
        Assert.Equal("ridge.png", stored.OriginalFileName);
    }

    /// <summary>
    /// The migrated size column is a <c>bigint</c>, which the model test cannot say: it reads the
    /// model, and the model is not what a deployment creates. An <c>int</c> here would overflow a
    /// video over 2 GB and, before that, would have been a silent narrowing of the cap.
    /// </summary>
    [SkippableFact]
    public async Task The_migrated_size_column_is_a_64_bit_number()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        using IServiceScope scope = fixture.CreateScope();

        string? dataType = await ScalarAsync(
            scope.ServiceProvider.GetRequiredService<TrailBlazeContext>(),
            "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS "
            + "WHERE TABLE_NAME = 'Media' AND COLUMN_NAME = 'SizeBytes'");

        Assert.Equal("bigint", dataType);
    }

    /// <summary>
    /// A 200 MB row round-trips through the column the migration created, which is the only place the
    /// cap and the column are checked against each other.
    /// </summary>
    [SkippableFact]
    public async Task A_video_sized_item_is_not_narrowed_by_its_column()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string activityId = Guid.NewGuid().ToString();
        var written = Item(activityId, "ridge.mp4", Constant.MediaKind.Video, VideoSize);

        using IServiceScope scope = fixture.CreateScope();
        await Repository(scope).CreateAsync(written);

        using IServiceScope reading = fixture.CreateScope();

        Assert.Equal(
            VideoSize, (await Repository(reading).GetAsync<Media>(row => row.Id == written.Id))!.SizeBytes);
    }

    /// <summary>
    /// The engine refuses a kind outside the closed set, which is the migration's claim and not the
    /// model's: the check constraint is SQL the migration emitted, and this is SQL Server rejecting a
    /// row with it.
    /// </summary>
    [SkippableFact]
    public async Task The_engine_refuses_a_kind_outside_the_closed_set()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        using IServiceScope scope = fixture.CreateScope();

        var rogue = Item(Guid.NewGuid().ToString(), "ridge.mp3", Constant.MediaKind.Image, 4_096);
        rogue.Kind = "Audio";

        await Assert.ThrowsAsync<DbUpdateException>(() => Repository(scope).CreateAsync(rogue));
    }

    // ---- The per-contributor cap -----------------------------------------------------------

    /// <summary>
    /// The boundary the cap is: the last item under it lands and the next is refused, with the count
    /// taken over rows that are live.
    /// </summary>
    /// <remarks>
    /// Seeded to one below the limit rather than to the limit, so the two halves are the two answers
    /// a caller can get at the edge rather than two refusals.
    /// </remarks>
    [SkippableFact]
    public async Task The_last_admitted_item_lands_and_the_next_one_is_refused()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string activityId = Guid.NewGuid().ToString();
        await SeedAsync(activityId, Constant.MediaLimit.PerContributorPerActivity - 1);

        using IServiceScope scope = fixture.CreateScope();
        DatabaseRepository repository = Repository(scope);

        bool admitted = await repository.CreateIfUnderAsync(
            Item(activityId, "last-in.png", Constant.MediaKind.Image, 32),
            row => row.ActivityId == activityId && row.CreatedBy == Ada,
            Constant.MediaLimit.PerContributorPerActivity);

        bool refused = await repository.CreateIfUnderAsync(
            Item(activityId, "one-too-many.png", Constant.MediaKind.Image, 32),
            row => row.ActivityId == activityId && row.CreatedBy == Ada,
            Constant.MediaLimit.PerContributorPerActivity);

        Assert.True(admitted);
        Assert.False(refused);

        // The refusal wrote nothing, which is the half that matters: an insert that happened and
        // reported failure would still be a row over the cap.
        Assert.Equal(Constant.MediaLimit.PerContributorPerActivity, await CountAsync(activityId));
    }

    /// <summary>
    /// Five uploads racing for the last place in one contributor's allowance leave it full rather
    /// than over.
    /// </summary>
    /// <remarks>
    /// <b>The one test that fails if the isolation level is dropped.</b> Under read committed every
    /// racer counts the same rows, every one decides there is room, and the contributor ends over
    /// their cap; a serializable transaction is what makes the count and the insert one decision.
    /// How many racers are turned away, and whether one of them is a deadlock victim the engine
    /// picked, is not this test's subject — the count is, and it is bounded on both sides so the
    /// assertion cannot pass by nothing having happened.
    /// </remarks>
    [SkippableFact]
    public async Task Concurrent_uploads_at_the_boundary_cannot_exceed_the_cap()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string activityId = Guid.NewGuid().ToString();
        int seeded = Constant.MediaLimit.PerContributorPerActivity - 1;
        await SeedAsync(activityId, seeded);

        Task<bool>[] racers =
        [
            .. Enumerable.Range(0, 5).Select(index => Task.Run(async () =>
            {
                using IServiceScope scope = fixture.CreateScope();

                try
                {
                    return await Repository(scope).CreateIfUnderAsync(
                        Item(activityId, $"racer-{index}.png", Constant.MediaKind.Image, 32),
                        row => row.ActivityId == activityId && row.CreatedBy == Ada,
                        Constant.MediaLimit.PerContributorPerActivity);
                }
                catch (Exception)
                {
                    // A deadlock victim is a refusal the engine delivered as a fault. Counted as one.
                    return false;
                }
            })),
        ];

        bool[] answers = await Task.WhenAll(racers);
        int rows = await CountAsync(activityId);

        Assert.InRange(rows, seeded, Constant.MediaLimit.PerContributorPerActivity);

        // Every admittance wrote exactly one row and every refusal wrote none, which is what ties
        // the answers to the table rather than to each other.
        Assert.Equal(rows - seeded, answers.Count(admitted => admitted));
    }

    /// <summary>
    /// The count is the activity's, so a second activity is unaffected by the first one being full.
    /// </summary>
    [SkippableFact]
    public async Task One_activity_being_full_does_not_fill_another()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string full = Guid.NewGuid().ToString();
        string empty = Guid.NewGuid().ToString();
        await SeedAsync(full, Constant.MediaLimit.PerContributorPerActivity);

        using IServiceScope scope = fixture.CreateScope();

        bool admitted = await Repository(scope).CreateIfUnderAsync(
            Item(empty, "first.png", Constant.MediaKind.Image, 32),
            row => row.ActivityId == empty && row.CreatedBy == Ada,
            Constant.MediaLimit.PerContributorPerActivity);

        Assert.True(admitted);
    }

    /// <summary>
    /// The count is also the contributor's, so media is collaborative rather than a shared budget: a
    /// second contributor is admitted to an activity Ada has filled.
    /// </summary>
    [SkippableFact]
    public async Task One_contributor_being_full_does_not_fill_the_activity()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string activityId = Guid.NewGuid().ToString();
        await SeedAsync(activityId, Constant.MediaLimit.PerContributorPerActivity);

        using IServiceScope scope = fixture.CreateScope();

        bool admitted = await Repository(scope).CreateIfUnderAsync(
            Item(activityId, "grace-first.png", Constant.MediaKind.Image, 32, Grace),
            row => row.ActivityId == activityId && row.CreatedBy == Grace,
            Constant.MediaLimit.PerContributorPerActivity);

        Assert.True(admitted);
        Assert.Equal(1, await CountAsync(activityId, Grace));
    }

    /// <summary>
    /// A soft-deleted row stops counting, so removing an item from a full activity makes room for
    /// another. Without this the cap would be a lifetime budget rather than a bound on what the
    /// activity holds.
    /// </summary>
    [SkippableFact]
    public async Task An_item_removed_from_a_full_activity_frees_its_place()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string activityId = Guid.NewGuid().ToString();
        List<Media> seeded = await SeedAsync(activityId, Constant.MediaLimit.PerContributorPerActivity);

        using (IServiceScope deleting = fixture.CreateScope())
        {
            await Repository(deleting).DeleteAsync<Media>([seeded[0].Id]);
        }

        using IServiceScope scope = fixture.CreateScope();

        bool admitted = await Repository(scope).CreateIfUnderAsync(
            Item(activityId, "replacement.png", Constant.MediaKind.Image, 32),
            row => row.ActivityId == activityId && row.CreatedBy == Ada,
            Constant.MediaLimit.PerContributorPerActivity);

        Assert.True(admitted);
    }

    /// <summary>
    /// A removed item leaves the read path and stays a row, which is what separates the soft delete
    /// the repository performs from a delete. The activity's own rows are asserted in
    /// <see cref="ActivityRoundTripTests"/>; this is the media half, and it is what frees the
    /// contributor's place under the cap.
    /// </summary>
    [SkippableFact]
    public async Task A_removed_item_leaves_the_read_path_and_stays_in_the_table()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string activityId = Guid.NewGuid().ToString();

        using IServiceScope scope = fixture.CreateScope();
        DatabaseRepository repository = Repository(scope);
        var item = Item(activityId, "ridge.png", Constant.MediaKind.Image, 4_096);

        await repository.CreateAsync(item);
        await repository.DeleteAsync<Media>([item.Id]);

        Assert.Null(await repository.GetAsync<Media>(row => row.Id == item.Id));

        Media? withheld = await scope.ServiceProvider
            .GetRequiredService<TrailBlazeContext>()
            .Media
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == item.Id);

        Assert.NotNull(withheld);
        Assert.True(withheld.IsDeleted);
    }

    /// <summary>
    /// An activity's deletion does not reach its media. The delete is soft, so the pictures have to
    /// outlive it and come back with it when it is restored; the service no longer removes them, and
    /// nothing in the schema does either, since no foreign key exists. Both halves are asserted, so
    /// the test cannot pass by the activity having survived.
    /// </summary>
    [SkippableFact]
    public async Task An_activitys_deletion_leaves_its_media_alive()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        var activity = new Activity
        {
            Title = "Ridge walk",
            Location = "North ridge",
            ActivityDate = new DateOnly(2026, 3, 14),
            Type = Constant.ActivityType.Public,
            CreatedBy = Ada,
        };

        Media item;

        using (IServiceScope writing = fixture.CreateScope())
        {
            DatabaseRepository repository = Repository(writing);
            await repository.CreateAsync(activity);

            item = Item(activity.Id, "ridge.png", Constant.MediaKind.Image, 4_096);
            await repository.CreateAsync(item);
        }

        using (IServiceScope deleting = fixture.CreateScope())
        {
            await Repository(deleting).DeleteAsync<Activity>([item.ActivityId]);
        }

        using IServiceScope reading = fixture.CreateScope();

        Assert.Null(await Repository(reading).GetAsync<Activity>(row => row.Id == item.ActivityId));
        Assert.NotNull(await Repository(reading).GetAsync<Media>(row => row.Id == item.Id));
    }

    // ---- Scaffolding -----------------------------------------------------------------------

    private static DatabaseRepository Repository(IServiceScope scope) =>
        new(scope.ServiceProvider.GetRequiredService<TrailBlazeContext>());

    /// <summary>Rows written with the audit stamps the interceptor would have set, since the cap
    /// counts what is live rather than what was stamped.</summary>
    private async Task<List<Media>> SeedAsync(string activityId, int count, string createdBy = Ada)
    {
        List<Media> rows =
            [.. Enumerable.Range(0, count).Select(index => Item(activityId, $"{index}.png", Constant.MediaKind.Image, 32, createdBy))];

        using IServiceScope scope = fixture.CreateScope();
        await Repository(scope).CreateAsync(rows);

        return rows;
    }

    /// <summary>This contributor's live rows on one activity — the set the cap is taken over.</summary>
    private async Task<int> CountAsync(string activityId, string createdBy = Ada)
    {
        using IServiceScope scope = fixture.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<TrailBlazeContext>()
            .Media
            .CountAsync(row => row.ActivityId == activityId && row.CreatedBy == createdBy);
    }

    private static Media Item(
        string activityId,
        string fileName,
        string kind,
        long sizeBytes,
        string createdBy = Ada) => new()
        {
            ActivityId = activityId,
            CreatedBy = createdBy,
            Kind = kind,
            BlobPath = $"{activityId}/{Guid.NewGuid():N}{Path.GetExtension(fileName)}",
            ContentType = kind == Constant.MediaKind.Video ? "video/mp4" : "image/png",
            SizeBytes = sizeBytes,
            OriginalFileName = fileName,
        };

    /// <summary>A single value from one statement, over the context's own connection, so the answer
    /// comes from the engine rather than from EF's view of the model.</summary>
    private static async Task<string?> ScalarAsync(TrailBlazeContext context, string sql)
    {
        DbConnection connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        return (await command.ExecuteScalarAsync())?.ToString();
    }
}
