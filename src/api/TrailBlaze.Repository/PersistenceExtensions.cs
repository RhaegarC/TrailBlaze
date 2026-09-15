using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Interface.Repository;

namespace TrailBlaze.Repository
{
    public static class PersistenceExtensions
    {
        /// <summary>Registers <see cref="TrailBlazeContext"/> (UseNpgsql only when a connection
        /// string is present — preserves no-DB startup for /health and Swagger), the audit
        /// interceptor that stamps audit-log entries on save, and the repositories as scoped
        /// services by interface.</summary>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="connectionString">Resolved by the composition root from configuration;
        /// when null or blank the context is registered without a provider, so the host still
        /// starts for /health and Swagger.</param>
        public static IServiceCollection AddRepositoryPersistence(
            this IServiceCollection services,
            string? connectionString)
        {
            services.AddScoped<AuditSaveChangesInterceptor>();

            services.AddDbContext<TrailBlazeContext>((serviceProvider, options) =>
            {
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    options.UseNpgsql(connectionString)
                        .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
                }
            });

            services.AddScoped<IUserRepository, UserRepository>();

            return services;
        }
    }
}
