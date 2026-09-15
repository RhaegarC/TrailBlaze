namespace TrailBlaze.Repository
{
    using Microsoft.Extensions.DependencyInjection;
    using TrailBlaze.Interface.Infrastructure;

    public static class StorageExtensions
    {
        /// <summary>Registers the Azure Blob implementation of <see cref="IStorageService"/>.</summary>
        /// <remarks>
        /// A singleton, deliberately: the underlying client is thread-safe and holds a
        /// connection pool, so building one per request would throw that away and add a client
        /// construction to every media call. Configuration is resolved by the composition root
        /// and passed down as a string.
        /// </remarks>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="connectionString">Resolved by the composition root from configuration.</param>
        public static IServiceCollection AddBlobStorage(
            this IServiceCollection services,
            string connectionString)
        {
            services.AddSingleton<IStorageService>(_ => new AzureBlobStorageService(connectionString));

            return services;
        }
    }
}
