namespace TrailBlaze.Repository
{
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Azure.Storage.Sas;
    using TrailBlaze.Interface.Repository;

    /// <summary>
    /// The real <see cref="IStorageRepository"/>, over Azure Blob Storage. This is the only type in
    /// the solution that names an Azure storage type: every caller reaches storage through
    /// <see cref="IStorageRepository"/> and the container constants, so the vendor is confined to
    /// this file.
    /// </summary>
    /// <remarks>
    /// Unit tests never construct this — they inject the in-memory fake. Only the
    /// <c>Category=StorageIntegration</c> tier, which needs real credentials and is excluded
    /// when they are absent, exercises the code below.
    /// <para>
    /// It requires a <b>shared-key</b> connection string: <see cref="CreateReadUrlAsync"/> signs
    /// a SAS locally, which needs the account key. A connection string carrying only a SAS token,
    /// or none at all under a managed identity, cannot sign and will throw on first use — that
    /// is a real constraint of this implementation, not an oversight.
    /// </para>
    /// </remarks>
    public sealed class AzureBlobStorageRepository : IStorageRepository
    {
        private readonly BlobServiceClient _serviceClient;

        /// <param name="connectionString">Resolved by the composition root from configuration and
        /// passed down, so this layer stays configuration-agnostic (STANDARD §6).</param>
        public AzureBlobStorageRepository(string connectionString)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            _serviceClient = new BlobServiceClient(connectionString);
        }

        /// <inheritdoc/>
        public async Task<string> UploadAsync(
            string container,
            string path,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);

            BlobClient blob = GetBlobClient(container, path);

            // Stating the content type is what makes the object serve back as an image rather
            // than as an opaque download, so it travels with the bytes rather than being
            // guessed from the extension at read time.
            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            };

            await blob.UploadAsync(content, options, cancellationToken);
            return path;
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(
            string container,
            string path,
            CancellationToken cancellationToken = default)
        {
            // DeleteIfExists, not Delete: the caller asked for the object to not be there, and
            // it is not. A 404 here would turn an idempotent cleanup into a failure.
            await GetBlobClient(container, path)
                .DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public Task<Uri> CreateReadUrlAsync(
            string container,
            string path,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default)
        {
            if (lifetime <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifetime), lifetime, "The read URL lifetime must be positive.");
            }

            BlobClient blob = GetBlobClient(container, path);

            var sas = new BlobSasBuilder
            {
                BlobContainerName = container,
                BlobName = path,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(lifetime),
            };
            sas.SetPermissions(BlobSasPermissions.Read);

            // Throws when the client holds no shared key to sign with, which is the intended
            // loud failure rather than a URL that silently carries no authorization.
            return Task.FromResult(blob.GenerateSasUri(sas));
        }

        /// <inheritdoc/>
        public async Task<string> MoveAsync(
            string sourceContainer,
            string sourcePath,
            string destinationContainer,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            BlobClient source = GetBlobClient(sourceContainer, sourcePath);
            BlobClient destination = GetBlobClient(destinationContainer, destinationPath);

            // Copy, wait, then delete. The opposite order loses the object outright if the copy
            // fails; this order can at worst leave it in both containers, which is visible and
            // recoverable. The copy is server-side and within one account, so the source needs
            // no SAS of its own -- the shared key on this client authorizes it.
            CopyFromUriOperation copy = await destination.StartCopyFromUriAsync(source.Uri, cancellationToken: cancellationToken);
            await copy.WaitForCompletionAsync(cancellationToken);

            await source.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            return destinationPath;
        }

        private BlobClient GetBlobClient(string container, string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(container);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            return _serviceClient.GetBlobContainerClient(container).GetBlobClient(path);
        }
    }
}
