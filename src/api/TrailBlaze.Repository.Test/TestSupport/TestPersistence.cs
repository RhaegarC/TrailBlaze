namespace TrailBlaze.Repository.Test.TestSupport;

using Microsoft.Extensions.DependencyInjection;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Repository;

/// <summary>
/// Builds a context provider over a given connection string, through the registration the
/// application itself uses.
/// </summary>
/// <remarks>
/// <para>
/// This exists so the composition is written once. Both the schema path and the audit harness
/// need a context, and if each assembled its own <c>DbContextOptions</c> the tier would be
/// testing a composition the app never runs — while still passing, because the model and the
/// interceptor are the same either way. What would silently differ is the lifetime and the
/// registration, which is precisely where a real defect would live.
/// </para>
/// <para>
/// The provider is returned rather than a context because the caller decides the scope: the
/// audit harness reads its history back through a <em>second</em> scope, which is what makes
/// the assertion a round trip to the database rather than a read of the change tracker that
/// just wrote it.
/// </para>
/// </remarks>
internal static class TestPersistence
{
    /// <param name="connectionString">The database to open. A fixture passes its own, so a
    /// test class cannot see another's rows.</param>
    /// <param name="user">The caller every save in this provider is attributed to.</param>
    public static ServiceProvider Build(string connectionString, IUserContextService user)
    {
        var services = new ServiceCollection();
        services.AddSingleton(user);
        services.AddRepositoryPersistence(connectionString);

        return services.BuildServiceProvider();
    }
}
