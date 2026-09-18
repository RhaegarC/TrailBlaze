namespace TrailBlaze.Repository.Test.TestSupport;

using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Repository;

/// <summary>
/// A <see cref="TrailBlazeContext"/> that never opens a connection, for the tests whose
/// subject is the <em>model</em> rather than anything the database does with it.
/// </summary>
/// <remarks>
/// <para>
/// Column lengths, defaults, nullability, the provider name and the model-versus-snapshot
/// agreement are all answers EF can give before a socket is opened — they are metadata, not
/// data. Routing them through the container fixture would buy nothing and cost the only
/// coverage that survives on a machine with no Docker: they would skip exactly when a
/// developer is editing the model, which is when they matter most.
/// </para>
/// <para>
/// This is not the old unreachable-server tripwire under a new name. That harness asserted
/// its saves <em>failed</em> because it had to run interception without a server; this type
/// saves nothing, so nothing is being worked around. The port stays unreachable to make the
/// guarantee structural: an accidental query here fails loudly rather than reaching whatever
/// happens to be listening on 1433 — which on a developer's machine running the test
/// containers is a real database.
/// </para>
/// <para>
/// The context is built through <c>AddRepositoryPersistence</c>, the same entry point the
/// application uses, rather than by assembling <c>DbContextOptions</c> here. A builder that
/// configured its own options would be asserting against a composition the app never runs —
/// and the provider-name test below exists precisely to catch that registration changing.
/// </para>
/// </remarks>
public sealed class OfflineContext : IDisposable
{
    /// <summary>Port 1 on loopback: nothing listens there, so a query that slipped through
    /// is refused immediately instead of finding a container.</summary>
    private const string UnreachableConnection =
        "Server=127.0.0.1,1;Database=TrailBlaze;User Id=sa;"
        + "Password=NotARealPassword!1;TrustServerCertificate=True;Connect Timeout=1";

    private readonly ServiceProvider _provider;

    public TrailBlazeContext Context { get; }

    public OfflineContext()
    {
        var services = new ServiceCollection();

        // The interceptor resolves the caller even when nothing is being audited, so the
        // ambient dependency has to be present for the context to be constructible.
        services.AddSingleton<IUserContextService>(FakeUserContext.NoRequest());
        services.AddRepositoryPersistence(UnreachableConnection);

        _provider = services.BuildServiceProvider();
        Context = _provider.GetRequiredService<TrailBlazeContext>();
    }

    public void Dispose()
    {
        Context.Dispose();
        _provider.Dispose();
    }
}
