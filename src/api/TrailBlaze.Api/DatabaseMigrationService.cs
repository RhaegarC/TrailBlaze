namespace TrailBlaze.Api
{
    using Microsoft.EntityFrameworkCore;
    using TrailBlaze.Repository;

    /// <summary>
    /// Applies EF Core migrations when the host starts, so a deployed environment never serves
    /// traffic against a schema it has not caught up with.
    /// </summary>
    /// <remarks>
    /// A hosted service rather than a call in <c>Program</c> for one reason: it is a seam. The
    /// API tests boot the real pipeline through <c>WebApplicationFactory</c>, and a test that
    /// only wants <c>/health</c> has no database to migrate — removing this registration is how
    /// it says so, without the production path growing a "skip migrations in tests" branch that
    /// would then be the untested path in production.
    /// </remarks>
    internal sealed class DatabaseMigrationService(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseMigrationService> logger) : IHostedService
    {
        /// <summary>The context is scoped, so it is resolved from a scope of its own rather than
        /// injected into this singleton.</summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            TrailBlazeContext context = scope.ServiceProvider.GetRequiredService<TrailBlazeContext>();

            await context.Database.MigrateAsync(cancellationToken);

            logger.LogInformation("Applied EF Core migrations.");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
