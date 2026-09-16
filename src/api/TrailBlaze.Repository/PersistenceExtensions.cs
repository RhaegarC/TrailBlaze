using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Interface.Repository;

namespace TrailBlaze.Repository
{
    public static class PersistenceExtensions
    {
        /// <summary>Registers <see cref="TrailBlazeContext"/> against SQL Server (the engine
        /// Azure SQL Server speaks) when a connection string is present, the audit interceptor
        /// that stamps audit-log entries on save, and the repository as a scoped service by its
        /// interface.</summary>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="connectionString">Resolved by the composition root from configuration,
        /// which refuses to start without <c>DbConnection</c> (STANDARD §6) — so this is never
        /// blank by the time it arrives. Tests pass an unreachable but well-formed string, which
        /// is enough because no test opens a connection (STANDARD §10).</param>
        public static IServiceCollection AddRepositoryPersistence(
            this IServiceCollection services,
            string connectionString)
        {
            services.AddScoped<AuditSaveChangesInterceptor>();

            services.AddDbContext<TrailBlazeContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(connectionString)
                    .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
            });

            services.AddScoped<IDbRepository, DatabaseRepository>();

            return services;
        }
    }
}
