using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository;

namespace TrailBlaze.Tests.TestSupport
{
    /// <summary>
    /// A <c>TrailBlazeContext</c> with the audit interceptor attached, wired the way the application
    /// wires it — through <see cref="PersistenceExtensions.AddRepositoryPersistence"/> rather
    /// than by hand, so the tests exercise the same composition the app uses.
    /// </summary>
    internal sealed class AuditHarness : IDisposable
    {
        /// <summary>
        /// Nothing listens on port 1, so a save fails as soon as EF opens a connection.
        /// Interception runs before that, so the audit entries are already in the change
        /// tracker by the time the failure happens. That is what makes these tests run without
        /// a database.
        /// </summary>
        public const string UnreachableConnectionString =
            "Host=127.0.0.1;Port=1;Database=probe;Username=probe;Password=probe;Timeout=1";

        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public FakeUserContext User { get; }

        public TrailBlazeContext Context { get; }

        public AuditHarness(FakeUserContext? user = null)
        {
            User = user ?? new FakeUserContext();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IUserContextService>(User);
            services.AddRepositoryPersistence(UnreachableConnectionString);

            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();
            Context = _scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();
        }

        /// <summary>Every audit entry the interceptor has staged so far.</summary>
        public IReadOnlyList<AuditLog> Recorded() =>
            Context.ChangeTracker.Entries<AuditLog>().Select(entry => entry.Entity).ToList();

        /// <summary>The single audit entry staged, failing if there is not exactly one.</summary>
        public AuditLog SingleEntry() => Assert.Single(Recorded());

        /// <summary>Saves and discards the expected connection failure.</summary>
        public void Save() => Assert.ThrowsAny<Exception>(() => Context.SaveChanges());

        /// <inheritdoc cref="Save"/>
        public Task SaveAsync() =>
            Assert.ThrowsAnyAsync<Exception>(() => Context.SaveChangesAsync());

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
        }
    }
}
