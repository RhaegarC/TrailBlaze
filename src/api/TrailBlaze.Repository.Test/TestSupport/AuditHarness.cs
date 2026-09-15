namespace TrailBlaze.Repository.Test.TestSupport
{
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using TrailBlaze.Interface.Infrastructure;
    using TrailBlaze.Model.DatabaseEntity;
    using TrailBlaze.Repository;

    /// <summary>
    /// Exercises the persistence tier with **no database at all** (STANDARD §10).
    /// </summary>
    /// <remarks>
    /// <para>
    /// EF Core runs save interception before it opens a connection, so by the time a save fails
    /// against an unreachable server the audit rows are already staged in the change tracker —
    /// which is enough to assert audit stamping, soft-delete filtering and application-assigned
    /// keys without a container, a connection string, or credentials.
    /// </para>
    /// <para>
    /// The context is built through the same <c>AddRepositoryPersistence</c> entry point the
    /// application uses, rather than by hand, so the interceptor and the model configuration
    /// under test are the ones that ship. A harness that assembled its own
    /// <c>DbContextOptions</c> would be testing a composition the app never runs.
    /// </para>
    /// </remarks>
    public sealed class AuditHarness : IDisposable
    {
        /// <summary>Port 1 on loopback: nothing listens there, so the connection is refused
        /// immediately and the tier stays fast and offline.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=TrailBlaze;User Id=sa;"
            + "Password=NotARealPassword!1;TrustServerCertificate=True;Connect Timeout=1";

        private readonly ServiceProvider _provider;

        /// <summary>The caller the tier should reason about. Set it before saving.</summary>
        public FakeUserContext User { get; }

        public TrailBlazeContext Context { get; }

        /// <param name="user">The caller to attribute the change to. Defaults to
        /// <see cref="FakeUserContext.NoRequest"/>, the startup-and-background-jobs case.</param>
        public AuditHarness(FakeUserContext? user = null)
        {
            User = user ?? FakeUserContext.NoRequest();

            var services = new ServiceCollection();
            services.AddSingleton<IUserContextService>(User);
            services.AddRepositoryPersistence(UnreachableConnection);

            _provider = services.BuildServiceProvider();
            Context = _provider.GetRequiredService<TrailBlazeContext>();
        }

        /// <summary>The audit rows the interceptor staged, read from the change tracker rather
        /// than queried — there is no database to query.</summary>
        public IReadOnlyList<AuditLog> AuditEntries =>
            Context.ChangeTracker.Entries<AuditLog>().Select(entry => entry.Entity).ToList();

        /// <summary>The one audit row this save produced. Fails if the save produced a different
        /// number, which is what catches an interceptor that fires twice.</summary>
        public AuditLog SingleAuditEntry() => Assert.Single(AuditEntries);

        /// <summary>The audit row recorded for a given table.</summary>
        public AuditLog AuditEntryFor(string tableName) =>
            Assert.Single(AuditEntries, entry => entry.TableName == tableName);

        /// <summary>
        /// Saves, expecting the connection to fail <i>after</i> interception has run.
        /// </summary>
        /// <remarks>
        /// The failure is asserted rather than swallowed: a save that succeeded would mean this
        /// harness had reached a real SQL Server, and every test in the tier would silently stop
        /// being an offline test. Anything that is not a connection failure propagates, so a
        /// mapping or interceptor bug cannot hide behind the expected exception.
        /// </remarks>
        public void Save()
        {
            try
            {
                Context.SaveChanges();
            }
            catch (Exception exception) when (IsConnectionFailure(exception))
            {
                return;
            }

            throw new InvalidOperationException(
                "Expected the save to fail against the unreachable database, but it succeeded. "
                + "This harness must never reach a real server.");
        }

        /// <summary>The async counterpart of <see cref="Save"/>, covering the
        /// <c>SaveChangesAsync</c> interception path.</summary>
        public async Task SaveAsync()
        {
            try
            {
                await Context.SaveChangesAsync();
            }
            catch (Exception exception) when (IsConnectionFailure(exception))
            {
                return;
            }

            throw new InvalidOperationException(
                "Expected the save to fail against the unreachable database, but it succeeded. "
                + "This harness must never reach a real server.");
        }

        /// <summary>Walks the chain because EF wraps provider failures to varying depths
        /// depending on where in the save the connection was needed.</summary>
        private static bool IsConnectionFailure(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is SqlException)
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            Context.Dispose();
            _provider.Dispose();
        }
    }
}
