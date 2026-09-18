namespace TrailBlaze.Repository.Test;

using System.Net;
using TrailBlaze.Model;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The only tier that can speak about the real <c>IStorageRepository</c> implementation:
/// a minted signature is accepted by the server, the object comes back with its content type,
/// and the delete really removes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is distinct here is that the signature works.</b>
/// <c>StorageContainerRoutingTests</c> reads a SAS out of the URL and checks its shape and its
/// expiry; neither proves the server accepts it. Signing is computed locally from the account
/// key, so a wrong key, a wrong resource string or a permissions mistake all produce a
/// perfectly well-formed URL that is refused on fetch — the failure is invisible until
/// something fetches, which is what this does.
/// </para>
/// <para>
/// It runs against the Azurite emulator by default, which needs no credentials, and against a
/// real account when <c>TRAILBLAZE_STORAGE_CONNECTION</c> names one. Both reach the same code;
/// only the account behind it differs.
/// </para>
/// </remarks>
[Trait("Category", "Container")]
public sealed class AzureBlobStorageIntegrationTests(AzureStorageFixture fixture)
    : IClassFixture<AzureStorageFixture>
{
    /// <summary>
    /// A URL minted for a private object fetches it, carrying the content type it was stored
    /// with, and stops fetching it once the object is gone.
    /// </summary>
    /// <remarks>
    /// The second half is what proves the delete happened. The URL outlives the object it names
    /// — a SAS is a signature, not a handle — so asking for it again cannot be read as success
    /// merely because the signature is still valid.
    /// </remarks>
    [SkippableFact]
    public async Task A_minted_url_reads_a_private_object_back_and_dies_with_it()
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        const string Container = Constant.StorageContainer.Media;
        string path = $"integration/{Guid.NewGuid():N}.txt";
        byte[] content = "trail-blaze"u8.ToArray();

        try
        {
            await storage.UploadAsync(Container, path, new MemoryStream(content), "text/plain");

            Uri readUrl = await storage.CreateReadUrlAsync(
                Container, path, TimeSpan.FromMinutes(5));

            using var http = new HttpClient();

            // Content type is asserted here or nowhere: the deleted unit-tier fake discarded
            // it, so nothing else in the suite would notice the upload losing its headers and
            // serving every image as an opaque download.
            HttpResponseMessage response = await http.GetAsync(readUrl);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(content, await response.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            await storage.DeleteAsync(Container, path);
        }

        Uri staleUrl = await storage.CreateReadUrlAsync(Container, path, TimeSpan.FromMinutes(5));

        using var httpClient = new HttpClient();
        HttpResponseMessage afterDelete = await httpClient.GetAsync(staleUrl);

        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    /// <summary>
    /// Deleting an object that is already gone is a cleanup, not a failure.
    /// </summary>
    /// <remarks>
    /// The caller asked for the object to not be there, and it is not. This is the one claim
    /// that needs a real backend to be worth anything: the deleted fake implemented the
    /// contract it was asserting, so it proved only that a dictionary tolerates a missing key.
    /// The implementation reaches this with <c>DeleteIfExists</c> rather than <c>Delete</c>,
    /// and swapping one for the other is a one-word change that would turn every idempotent
    /// cleanup into an exception.
    /// </remarks>
    [SkippableFact]
    public async Task A_delete_of_something_absent_is_not_an_error()
    {
        // Resolved before the recorder, and that ordering is load-bearing. Repository() skips
        // by throwing a SkipException when no account answers, and Xunit.Record.ExceptionAsync
        // catches everything -- so putting it inside would turn the skip into an exception the
        // assertion then reports as a failure, and the tier would go red on a machine that
        // simply has no container running.
        AzureBlobStorageRepository storage = fixture.Repository();

        Exception? thrown = await Record.ExceptionAsync(
            () => storage.DeleteAsync(
                Constant.StorageContainer.Media, $"integration/{Guid.NewGuid():N}.txt"));

        Assert.Null(thrown);
    }
}
