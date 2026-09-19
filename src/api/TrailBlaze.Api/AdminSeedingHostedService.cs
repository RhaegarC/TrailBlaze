namespace TrailBlaze.Api;

using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model.Admin;

/// <summary>
/// Runs <see cref="IAdminSeedingService"/> once, as the host starts, and records what it found.
/// </summary>
/// <remarks>
/// <para>
/// <b>Configuration absence stops the host; database absence does not.</b> The two are checked
/// in different places and that is the whole of the design. A missing or over-long admin
/// setting is refused at the composition root, before anything here runs, naming the key — a
/// deployment with no administrator is unadministrable and must not start looking healthy. A
/// database that cannot be reached is caught below, logged at error, and survived.
/// </para>
/// <para>
/// <b>Why surviving is right, and what it costs.</b> Every request this API serves reads or
/// writes the database, so a replica whose database is unreachable is useless whether or not it
/// finishes starting; the choice is only between a process that is up and logging and one that
/// exits and is restarted into the same state. Azure Container Apps starts several replicas
/// against a database that may still be waking, and a container that crash-loops on that never
/// gets the chance to seed on a later attempt without a full redeploy. The cost is real and
/// stated rather than hidden: this API can be running with no administrator in it, and the only
/// signal is the error line below. What keeps that from being silent is that it is logged at
/// error on every start it happens on, and that nothing else about the app works in that state
/// either.
/// </para>
/// <para>
/// <b>The catch is deliberately broad.</b> It is not there to hide a defect in the seeder —
/// those are logged with their stack and are the reason this is an error rather than a warning.
/// Narrowing it by exception type would mean naming a database provider's exceptions in the
/// entrance, which this layer reaches only transitively, and would buy nothing: there is no
/// failure of "put the configured administrator in the database" that this process should die
/// for.
/// </para>
/// <para>
/// <b>The scope is not optional.</b> A hosted service is a singleton and the seeder is scoped —
/// it holds a <c>DbContext</c> — so the work runs in a scope of its own, created and disposed
/// here rather than held for the life of the process.
/// </para>
/// </remarks>
internal sealed class AdminSeedingHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AdminSeedingHostedService> logger) : IHostedService
{
    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();

            IAdminSeedingService seeder =
                scope.ServiceProvider.GetRequiredService<IAdminSeedingService>();

            AdminSeedOutcome outcome = await seeder.SeedAdminAsync();

            logger.LogInformation("Administrator seeding finished: {Outcome}.", outcome);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Administrator seeding did not complete, so this instance has no administrator "
                + "unless an earlier start seeded one. The host is starting anyway: without the "
                + "database it is unusable regardless, and a replica that exits here cannot seed "
                + "on the next attempt without a redeploy.");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
