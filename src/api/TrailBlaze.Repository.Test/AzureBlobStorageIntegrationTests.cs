namespace TrailBlaze.Repository.Test
{
    using Azure.Storage.Blobs;
    using TrailBlaze.Model;

    /// <summary>
    /// The only tier that can speak about the real <c>IStorageRepository</c> implementation:
    /// upload, read back through a minted URL, and delete, against a live Azure Storage account.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Azure is a real cloud resource in every environment (PRD Decision #5), so this needs
    /// credentials. It is tagged <c>Category=StorageIntegration</c> and skips — never fails —
    /// when the connection string is absent, so the default <c>dotnet test</c> stays green and
    /// offline on a machine that has no account.
    /// </para>
    /// <para>
    /// Run it explicitly with credentials in the environment:
    /// <c>TRAILBLAZE_STORAGE_CONNECTION="..." dotnet test --filter Category=StorageIntegration</c>
    /// </para>
    /// <para>
    /// A unit test that stays inside the in-memory fake proves the caller's logic and nothing
    /// about this code — SAS generation, content-type round-tripping and container existence
    /// live here or nowhere.
    /// </para>
    /// </remarks>
    [Trait("Category", "StorageIntegration")]
    public sealed class AzureBlobStorageIntegrationTests
    {
        private const string ConnectionStringVariable = "TRAILBLAZE_STORAGE_CONNECTION";

        private static string? ConnectionString =>
            Environment.GetEnvironmentVariable(ConnectionStringVariable);

        [SkippableFact]
        public async Task An_upload_reads_back_through_a_minted_url_and_then_deletes()
        {
            Skip.IfNot(
                !string.IsNullOrWhiteSpace(ConnectionString),
                $"Set {ConnectionStringVariable} to run the storage integration tier.");

            var storage = new AzureBlobStorageRepository(ConnectionString!);
            string container = Constant.StorageContainer.Media;
            await EnsureContainerExistsAsync(ConnectionString!, container);

            // A path per run, so a failed run cannot be mistaken for the next one's leftovers.
            string path = $"integration/{Guid.NewGuid():N}.txt";
            byte[] content = "trail-blaze"u8.ToArray();

            try
            {
                await storage.UploadAsync(container, path, new MemoryStream(content), "text/plain");

                Uri readUrl = await storage.CreateReadUrlAsync(container, path, TimeSpan.FromMinutes(5));
                using var http = new HttpClient();

                // Content type is asserted here or nowhere: the unit-tier fake discards it, so
                // nothing else in the suite would notice the upload losing its headers and
                // serving every image as an opaque download.
                HttpResponseMessage response = await http.GetAsync(readUrl);
                Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
                Assert.Equal(content, await response.Content.ReadAsByteArrayAsync());
            }
            finally
            {
                await storage.DeleteAsync(container, path);
            }

            // The URL outlives the object, so asking for it again must now be a 404 rather than
            // a stale success -- which is what proves the delete really happened.
            Uri staleUrl = await storage.CreateReadUrlAsync(container, path, TimeSpan.FromMinutes(5));
            using var httpClient = new HttpClient();
            HttpResponseMessage afterDelete = await httpClient.GetAsync(staleUrl);

            Assert.Equal(System.Net.HttpStatusCode.NotFound, afterDelete.StatusCode);
        }

        /// <summary>
        /// The production implementation reads and writes objects but does not provision the
        /// containers, which are a deployment concern. The test therefore creates its own.
        /// </summary>
        private static async Task EnsureContainerExistsAsync(string connectionString, string container)
        {
            var client = new BlobServiceClient(connectionString);
            await client.GetBlobContainerClient(container).CreateIfNotExistsAsync();
        }
    }
}
