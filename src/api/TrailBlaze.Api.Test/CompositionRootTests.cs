namespace TrailBlaze.Api.Test;

using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;

/// <summary>
/// That everything the composition root registers can actually be built.
/// </summary>
/// <remarks>
/// <para>
/// A container resolves lazily, so a registration that cannot be constructed is not a startup
/// failure — it is a 500 on the first request that reaches the affected route, in an
/// environment where the failure is hardest to read. Booting the host runs none of this: the
/// tests that assert startup behaviour never ask for these types, and would pass over a
/// registration naming an interface with no implementation.
/// </para>
/// <para>
/// No database is reached. Resolving the context constructs it against the factory's
/// unreachable connection string without opening a connection, the same way the rest of this
/// tier stays offline.
/// </para>
/// <para>
/// <b>What that leaves unproven, said plainly.</b> Resolving a registration is not using it,
/// so nothing here shows that this composition root can open a connection and read through it.
/// A lifetime mistake — a <c>DbContext</c> held longer than its scope, say — is invisible to a
/// <c>GetRequiredService</c> call and would surface only under a real request. The repository
/// tier exercises the same registration against a live database through
/// <c>TrailBlaze.Repository.Test</c>'s own <c>TestPersistence.Build</c>, which is a different
/// entry point onto the same extension method; the composition root itself has never been
/// asked to talk to a server. Recorded in the debt register rather than fixed here, because
/// closing it means giving this tier a database dependency it was deliberately built without.
/// </para>
/// </remarks>
public sealed class CompositionRootTests
{
    [Fact]
    public void Every_service_the_composition_root_registers_resolves()
    {
        using var factory = new TrailBlazeApiFactory();
        using HttpClient client = factory.CreateClient();

        using IServiceScope scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDbRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStorageRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IActivityService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMediaService>());

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUploadValidationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserContextService>());
    }
}
