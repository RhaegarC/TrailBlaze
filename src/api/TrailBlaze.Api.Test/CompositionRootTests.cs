namespace TrailBlaze.Api.Test;

using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;

/// <summary>
/// That everything the composition root registers can actually be built.
/// </summary>
/// <remarks>
/// A container resolves lazily, so a registration that cannot be constructed is not a startup
/// failure — it is a 500 on the first request that reaches the affected route, in an
/// environment where the failure is hardest to read. Booting the host runs none of this: the
/// tests that assert startup behaviour never ask for these types, and would pass over a
/// registration naming an interface with no implementation.
/// <para>
/// No database is reached. Resolving the context constructs it against the factory's
/// unreachable connection string without opening a connection, the same way the rest of this
/// tier stays offline.
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
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUploadValidationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserContextService>());
    }
}
