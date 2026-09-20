namespace TrailBlaze.Repository.Test.TestSupport;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// Exercises the persistence tier against a real SQL Server: saves through the application's
/// own registration and reads the history back out of the database.
/// </summary>
/// <remarks>
/// <para>
/// <b>What changed and why.</b> This harness used to point EF at an unreachable server and
/// assert that every save <em>failed</em>, because save interception runs before a connection
/// is opened and the staged audit rows were therefore readable without a database. That was
/// clever and it cost real coverage: it could not execute a migration, could not survive an
/// <c>UPDATE</c> against a missing row, and read its history out of the change tracker that
/// had just written it — so "the row was recorded" meant "the interceptor staged something",
/// not "the database holds it".
/// </para>
/// <para>
/// Nothing is lost by its going. The one property it did prove — that EF runs interception
/// before opening a connection — is an EF ordering guarantee, not a TrailBlaze behaviour, and
/// is unobservable against a live server. The tripwire it carried ("this must never reach a
/// real server") is replaced by the fixture's skip, which is strictly more visible: the old
/// one could only fire if a save unexpectedly succeeded.
/// </para>
/// <para>
/// The context is still built through <c>AddRepositoryPersistence</c> rather than by hand, so
/// the interceptor, the model and the registration under test are the ones that ship.
/// </para>
/// </remarks>
public sealed class AuditHarness : IDisposable
{
    private readonly ServiceProvider _provider;

    /// <summary>The caller the tier should reason about. Set it before saving.</summary>
    public FakeUserContext User { get; }

    /// <summary>The context this harness saves through. Rows written here are committed to
    /// the fixture's database, so a second test in the same class sees them — which is why
    /// every read below is filtered by entity id.</summary>
    public TrailBlazeContext Context { get; }

    /// <param name="connectionString">The fixture's own database.</param>
    /// <param name="user">The caller to attribute the change to. Defaults to
    /// <see cref="FakeUserContext.NoRequest"/>, the startup-and-background-jobs case.</param>
    public AuditHarness(string connectionString, FakeUserContext? user = null)
    {
        User = user ?? FakeUserContext.NoRequest();
        _provider = TestPersistence.Build(connectionString, User);
        Context = _provider.GetRequiredService<TrailBlazeContext>();
    }

    /// <summary>
    /// The history rows recorded for one entity.
    /// </summary>
    /// <remarks>
    /// Read through a <em>new</em> scope, which is the point: a fresh context has an empty
    /// change tracker, so the rows come back from the database rather than from the tracker
    /// that just wrote them. That distinction is what makes this an assertion about
    /// persistence instead of about EF's in-memory state.
    /// <para>
    /// Filtered by entity id because every test in a class shares one database, so an
    /// unfiltered read would find the previous test's rows. Ordered by timestamp for
    /// readability only — two rows from one fast test can share a tick, so a test that needs
    /// a specific row asks for it by action via <see cref="AuditEntryAsync"/>.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<AuditLog>> AuditEntriesAsync(string entityId)
    {
        using IServiceScope scope = _provider.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        return await context.AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityId == entityId)
            .OrderBy(log => log.Timestamp)
            .ToListAsync();
    }

    /// <summary>The one history row this save produced. Fails if the save produced a
    /// different number, which is what catches an interceptor that fires twice.</summary>
    public async Task<AuditLog> SingleAuditEntryAsync(string entityId) =>
        Assert.Single(await AuditEntriesAsync(entityId));

    /// <summary>The one history row recorded for an entity by a given action. An entity that
    /// was inserted and then changed has two rows, and this is how a test names the one it
    /// means without depending on which tick each landed in.</summary>
    public async Task<AuditLog> AuditEntryAsync(string entityId, string action)
    {
        IReadOnlyList<AuditLog> entries = await AuditEntriesAsync(entityId);

        return Assert.Single(entries, entry => entry.Action == action);
    }

    /// <summary>A user as the database holds it, read through a fresh scope — the stored row
    /// rather than the tracked one the interceptor stamped.</summary>
    public async Task<User?> FindUserAsync(string id)
    {
        using IServiceScope scope = _provider.CreateScope();
        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

        return await context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id);
    }

    public void Save() => Context.SaveChanges();

    /// <summary>The async counterpart of <see cref="Save"/>, covering the
    /// <c>SaveChangesAsync</c> interception path.</summary>
    public Task SaveAsync() => Context.SaveChangesAsync();

    public void Dispose()
    {
        Context.Dispose();
        _provider.Dispose();
    }
}
