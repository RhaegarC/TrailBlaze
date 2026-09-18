namespace TrailBlaze.Repository.Test.TestSupport;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Repository;

/// <summary>
/// The migrated database: this solution's migration set applied to an empty database. What
/// every test but the narrowing one wants.
/// </summary>
public sealed class TrailBlazeDatabaseFixture : DatabaseFixtureBase
{
    protected override async Task ApplySchemaAsync()
    {
        // Migrate, never EnsureCreated. EnsureCreated builds the schema from the model and
        // bypasses Migrations/ entirely, so a broken migration stays broken and the suite
        // still passes -- a test that cannot fail. It would also close the door behind it: a
        // database built that way has no __EFMigrationsHistory, so a later Migrate would try
        // to create tables that already exist.
        //
        // Running the migration set is the whole point of this tier. Before it, no test had
        // ever executed one: PersistenceModelTests compares the model to the in-memory
        // snapshot, and SoftDeleteFilterTests reads generated SQL. Two artefacts agreeing
        // with each other, neither ever applied -- which is exactly how Npgsql-shaped
        // migrations once shipped (archived item 03).
        await using ServiceProvider provider = BuildProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// The harness the audit tier saves through, or a skip naming why it cannot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It skips rather than returning null, so a test cannot forget to check availability and
    /// fail with a null reference that says nothing about the missing container. The skip
    /// surfaces as a skipped test carrying <see cref="DatabaseFixtureBase.SkipReason"/>.
    /// </para>
    /// <para>
    /// <b>The calling test must be <c>[SkippableFact]</c>, not <c>[Fact]</c>.</b>
    /// <see cref="Skip.IfNot"/> works by throwing <c>Xunit.SkipException</c>, and only
    /// SkippableFact's runner turns that into a skip — under a plain <c>[Fact]</c> it is just
    /// an exception, so the test <em>fails</em> with the skip message in its output. That is
    /// the exact opposite of "skip when the container is unreachable, never fail", and it is
    /// silent in the sense that matters: the run goes red for a reason that looks like a
    /// missing configuration rather than a mis-declared attribute.
    /// </para>
    /// </remarks>
    public AuditHarness CreateHarness(FakeUserContext? user = null)
    {
        Skip.IfNot(IsAvailable, SkipReason);
        return new AuditHarness(DatabaseConnectionString, user);
    }
}
