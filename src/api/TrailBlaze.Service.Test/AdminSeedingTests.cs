namespace TrailBlaze.Service.Test;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Model;
using TrailBlaze.Model.Admin;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// Seeding the one administrator, against a real engine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is here rather than in the offline tier.</b> Every property below is about what
/// a second call does to a stored row, and this suite has no fake for the database —
/// deliberately, because a fake would implement the contract it was asserting. So the
/// assertions are made against SQL Edge, and the fixture that creates and drops a database per
/// class lives in the repository tier. This project references it for that reason and no other;
/// see the project file.
/// </para>
/// <para>
/// <b>One database, distinct keys.</b> The fixture migrates one database per class, and every
/// test below seeds an object id of its own, so nothing here reads another test's rows. That is
/// also why every read is by id rather than by count of the whole table: <c>Users</c> is shared
/// with whatever else this class has run.
/// </para>
/// </remarks>
[Trait("Category", "Container")]
public sealed class AdminSeedingTests(TrailBlazeDatabaseFixture fixture)
    : IClassFixture<TrailBlazeDatabaseFixture>
{
    /// <summary>The name the configured admin is seeded with.</summary>
    private const string ConfiguredName = "Configured Admin";

    /// <summary>
    /// An empty database gains exactly one row, and it is the administrator the configuration
    /// named.
    /// </summary>
    [SkippableFact]
    public async Task Seeding_creates_the_configured_admin()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string objectId = Guid.NewGuid().ToString();

        using (IServiceScope scope = fixture.CreateScope())
        {
            Assert.Equal(AdminSeedOutcome.Inserted, await SeederOver(scope, objectId).SeedAdminAsync());
        }

        User stored = await SingleUserAsync(objectId);

        Assert.Equal(Constant.UserRole.Admin, stored.Role);
        Assert.Equal(ConfiguredName, stored.DisplayName);
    }

    /// <summary>
    /// Running it again reports that the work was already done, and writes nothing at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Idempotence is the whole requirement, and "one row" is a weak way to assert it: a
    /// seeder that re-saved the row on every start would also leave one row. So the assertions
    /// are about the absence of a write. The audit trail is the strongest of them — it is
    /// written by the interceptor, which only runs when a save happens, so one entry after two
    /// runs means the second run reached the database not at all rather than reaching it and
    /// changing nothing.
    /// </para>
    /// <para>
    /// <c>LastModifiedOn</c> is the second witness, and it is a bounded one: the interceptor
    /// does not stamp it on insert (item 20), so on a row seeded moments ago it may hold
    /// nothing useful. It is compared rather than asserted on, so the test is red if a second
    /// run changes it and says nothing either way about what the first run left there.
    /// </para>
    /// </remarks>
    [SkippableFact]
    public async Task Seeding_a_second_time_writes_nothing()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string objectId = Guid.NewGuid().ToString();

        using (IServiceScope scope = fixture.CreateScope())
        {
            Assert.Equal(AdminSeedOutcome.Inserted, await SeederOver(scope, objectId).SeedAdminAsync());
        }

        DateTimeOffset afterFirstRun = (await SingleUserAsync(objectId)).LastModifiedOn;

        using (IServiceScope scope = fixture.CreateScope())
        {
            // The same process, twice — which is the restart case and the two-replicas case at
            // once, since neither is distinguishable from here.
            Assert.Equal(
                AdminSeedOutcome.AlreadyAdmin, await SeederOver(scope, objectId).SeedAdminAsync());
        }

        User stored = await SingleUserAsync(objectId);

        Assert.Equal(afterFirstRun, stored.LastModifiedOn);
        Assert.Single(await AuditEntriesAsync(objectId));
    }

    /// <summary>
    /// A row that already exists for the configured object id is promoted in place, and
    /// nothing else on it is touched.
    /// </summary>
    /// <remarks>
    /// The row this happens to is the one a sign-in provisioned before the deploy configured
    /// that person as the administrator. Everything on it was written by them — their display
    /// name, their avatar, their theme — and the deploy's only business here is the one column
    /// that is not theirs to choose. A seeder that replaced the row, or that re-saved the whole
    /// entity from configuration, would take all of that away as a side effect of granting a
    /// permission, silently and at deploy time.
    /// </remarks>
    [SkippableFact]
    public async Task Seeding_promotes_an_existing_row_without_resetting_it()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        string objectId = Guid.NewGuid().ToString();

        using (IServiceScope scope = fixture.CreateScope())
        {
            TrailBlazeContext context = ContextOf(scope);

            context.Users.Add(new User
            {
                Id = objectId,
                DisplayName = "Their Own Name",
                Email = "them@example.com",
                Description = "Written before the deploy.",
                PreferredTheme = Constant.UserPreference.LightTheme,
                PreferredLanguage = Constant.UserPreference.Chinese,
                Role = Constant.UserRole.User,
            });

            await context.SaveChangesAsync();
        }

        using (IServiceScope scope = fixture.CreateScope())
        {
            Assert.Equal(AdminSeedOutcome.Promoted, await SeederOver(scope, objectId).SeedAdminAsync());
        }

        User stored = await SingleUserAsync(objectId);

        Assert.Equal(Constant.UserRole.Admin, stored.Role);

        Assert.Equal("Their Own Name", stored.DisplayName);
        Assert.Equal("them@example.com", stored.Email);
        Assert.Equal("Written before the deploy.", stored.Description);
        Assert.Equal(Constant.UserPreference.LightTheme, stored.PreferredTheme);
        Assert.Equal(Constant.UserPreference.Chinese, stored.PreferredLanguage);
    }

    /// <summary>
    /// A seeder over one scope's repository and the configured administrator.
    /// </summary>
    private static AdminSeedingService SeederOver(IServiceScope scope, string objectId) =>
        new(scope.ServiceProvider.GetRequiredService<IDbRepository>(),
            new AdminSeed(objectId, ConfiguredName));

    private static TrailBlazeContext ContextOf(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

    /// <summary>
    /// The one row for an id, read through a scope of its own so the answer is the stored row
    /// rather than one this test's context is still tracking. <c>Single</c> rather than
    /// <c>SingleOrDefault</c>: a seeder that inserted alongside an existing row would be two
    /// rows here, and that is the defect this is looking for.
    /// </summary>
    private async Task<User> SingleUserAsync(string objectId)
    {
        using IServiceScope scope = fixture.CreateScope();

        return await ContextOf(scope)
            .Users.AsNoTracking()
            .SingleAsync(user => user.Id == objectId);
    }

    private async Task<IReadOnlyList<AuditLog>> AuditEntriesAsync(string objectId)
    {
        using IServiceScope scope = fixture.CreateScope();

        return await ContextOf(scope)
            .AuditLogs.AsNoTracking()
            .Where(log => log.EntityId == objectId)
            .ToListAsync();
    }
}
