namespace TrailBlaze.Repository.Test;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The migration set executed against a real engine: does it apply, does it leave the schema
/// the model describes, and does the schema hold what the code will write into it.
/// </summary>
/// <remarks>
/// Nothing here could be asserted before. <see cref="PersistenceModelTests"/> compares the
/// model to the in-memory snapshot and <see cref="UserModelTests"/> reads <c>IModel</c>
/// metadata — two artefacts agreeing with each other, neither ever applied to a database. That
/// is precisely how Npgsql-shaped migrations once shipped (archived item 03): the snapshot and
/// the model can agree perfectly while the SQL that carries them to a server does not work.
/// </remarks>
[Trait("Category", "Container")]
public sealed class DatabaseSchemaTests(TrailBlazeDatabaseFixture fixture)
    : IClassFixture<TrailBlazeDatabaseFixture>
{
    /// <summary>
    /// Every migration the assembly declares has been applied, and none is outstanding.
    /// </summary>
    /// <remarks>
    /// The fixture already runs <c>MigrateAsync</c>, so this failing means one of two things,
    /// and both are real: a migration in the set could not be applied, or someone added a
    /// migration to the model and never generated one — in which case the schema a deployment
    /// builds is not the schema the code expects.
    /// </remarks>
    [SkippableFact]
    public async Task The_migration_set_applies_and_leaves_nothing_pending()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        List<string> declared = context.Database.GetMigrations().ToList();
        List<string> applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();

        Assert.NotEmpty(declared);
        Assert.Equal(declared, applied);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    /// <summary>
    /// Each bounded column carries its bound in the database, not only in the model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="UserModelTests"/> asserts these lengths on <c>IModel</c>, which proves what
    /// the mapping says and nothing about what SQL Server was told. The two can diverge
    /// without either looking wrong: a fluent call added after the migration was generated
    /// leaves the model bounded and the column <c>nvarchar(max)</c>, and since
    /// <c>nvarchar(max)</c> rejects nothing, the assertion the length exists to support — that
    /// an over-long value is refused — would silently stop being true.
    /// </para>
    /// <para>
    /// <c>INFORMATION_SCHEMA</c> reports <c>NULL</c> for an unbounded column, reported here as
    /// <c>-1</c> so an unbounded column fails the comparison rather than skipping it.
    /// </para>
    /// </remarks>
    [SkippableTheory]
    [InlineData(nameof(User.DisplayName), Constant.UserProfile.DisplayNameLength)]
    [InlineData(nameof(User.Email), Constant.UserProfile.EmailLength)]
    [InlineData(nameof(User.Description), Constant.UserProfile.DescriptionLength)]
    [InlineData(nameof(User.AvatarBlobPath), Constant.UserProfile.AvatarBlobPathLength)]
    [InlineData(nameof(User.Role), Constant.UserProfile.RoleLength)]
    [InlineData(nameof(User.PreferredTheme), Constant.UserProfile.PreferenceLength)]
    [InlineData(nameof(User.PreferredLanguage), Constant.UserProfile.PreferenceLength)]
    public async Task The_model_bound_reached_the_database(string column, int expectedLength)
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        using IServiceScope scope = fixture.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        int length = await context.Database
            .SqlQueryRaw<int>(
                "SELECT ISNULL(CHARACTER_MAXIMUM_LENGTH, -1) AS [Value] "
                + "FROM INFORMATION_SCHEMA.COLUMNS "
                + "WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = {0}",
                column)
            .SingleAsync();

        Assert.Equal(expectedLength, length);
    }

    /// <summary>
    /// The audit snapshots hold a value far larger than any single write produces.
    /// </summary>
    /// <remarks>
    /// <see cref="PersistenceModelTests"/> asserts the columns are <c>nvarchar(max)</c>, which
    /// is a claim about the mapping. A column can be declared unbounded and still truncate —
    /// through a competing <c>ALTER</c>, a data-tier setting, or a provider that silently
    /// caps — and a truncated snapshot is the worst case for this table, because the audit
    /// trail is what someone reads when they are trying to work out what happened. Writing
    /// 5,000 characters and reading them back is the only way to know.
    /// <para>
    /// Written directly rather than produced by a save: no realistic edit yields a snapshot
    /// this size, and the subject here is the column's capacity rather than the interceptor.
    /// </para>
    /// </remarks>
    [SkippableFact]
    public async Task A_long_audit_snapshot_survives_the_round_trip()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        const int Length = 5_000;
        string oldValues = new('o', Length);
        string newValues = new('n', Length);
        string id = Guid.NewGuid().ToString();

        using (IServiceScope writing = fixture.CreateScope())
        {
            TrailBlazeContext context =
                writing.ServiceProvider.GetRequiredService<TrailBlazeContext>();

            context.AuditLogs.Add(new AuditLog
            {
                Id = id,
                Actor = "system",
                TableName = "Users",
                Action = "Added",
                OldValues = oldValues,
                NewValues = newValues,
                Timestamp = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync();
        }

        using IServiceScope reading = fixture.CreateScope();
        TrailBlazeContext reader =
            reading.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        AuditLog stored = await reader.AuditLogs
            .AsNoTracking()
            .SingleAsync(log => log.Id == id);

        Assert.Equal(Length, stored.OldValues!.Length);
        Assert.Equal(Length, stored.NewValues!.Length);

        // The length alone would pass if both columns came back with the same wrong value.
        Assert.Equal(oldValues, stored.OldValues);
        Assert.Equal(newValues, stored.NewValues);
    }
}
