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

            DateTimeOffset expiresOn = DateTimeOffset.UtcNow.AddMinutes(5);

            Uri readUrl = await storage.CreateReadUrlAsync(Container, path, expiresOn);

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

        Uri staleUrl = await storage.CreateReadUrlAsync(
            Container, path, DateTimeOffset.UtcNow.AddMinutes(5));

        using var httpClient = new HttpClient();
        HttpResponseMessage afterDelete = await httpClient.GetAsync(staleUrl);

        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    /// <summary>
    /// A signature whose expiry has passed is refused by the server, and the expiry is the only
    /// thing that has to be wrong for that to happen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The object is present and untouched, so nothing but the window separates this from the round
    /// trip above — which is what makes it an assertion about expiry rather than about a missing
    /// blob. The instant is in the past because the caller names it; waiting for a real window to
    /// lapse would make this test as slow as the window it is checking.
    /// </para>
    /// <para>
    /// A signed URL is computed locally from the account key, so this is the only tier that can tell
    /// a well-formed signature from an accepted one — and a signature that outlived its expiry is
    /// exactly the failure that looks like success everywhere else.
    /// </para>
    /// </remarks>
    [SkippableFact]
    public async Task A_signature_past_its_expiry_is_refused()
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        const string Container = Constant.StorageContainer.Media;
        string path = $"integration/{Guid.NewGuid():N}.txt";

        try
        {
            await storage.UploadAsync(
                Container, path, new MemoryStream("trail-blaze"u8.ToArray()), "text/plain");

            Uri expired = await storage.CreateReadUrlAsync(
                Container, path, DateTimeOffset.UtcNow.AddMinutes(-5));

            using var http = new HttpClient();
            HttpResponseMessage response = await http.GetAsync(expired);

            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await storage.DeleteAsync(Container, path);
        }
    }

    /// <summary>
    /// The expiry the token carries is the expiry the caller named, to the second.
    /// </summary>
    /// <remarks>
    /// The claim the service tier cannot finish on its own: it asserts that the instant it reports
    /// is the instant it handed to storage, and this asserts that storage signs that instant rather
    /// than one of its own choosing. Together they are "the expiry returned is the expiry embedded",
    /// which neither test makes alone.
    /// </remarks>
    [SkippableFact]
    public async Task The_token_carries_the_expiry_the_caller_named()
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        const string Container = Constant.StorageContainer.Media;
        string path = $"integration/{Guid.NewGuid():N}.txt";

        try
        {
            await storage.UploadAsync(
                Container, path, new MemoryStream("trail-blaze"u8.ToArray()), "text/plain");

            // Rounded the way the policy rounds, so the comparison is against the instant a SAS can
            // carry rather than against the sub-second part one cannot.
            DateTimeOffset rough = DateTimeOffset.UtcNow.AddMinutes(5);
            var named = new DateTimeOffset(
                rough.UtcTicks - (rough.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

            Uri url = await storage.CreateReadUrlAsync(Container, path, named);

            Assert.Equal(named, SignedUrl.ExpiryOf(url));
        }
        finally
        {
            await storage.DeleteAsync(Container, path);
        }
    }

    /// <summary>
    /// The server refuses a write made with a read URL, and the object it names is untouched.
    /// </summary>
    /// <remarks>
    /// A SAS is computed locally from the account key, so a permission set that never reached the
    /// builder produces a URL that looks exactly like a read-only one. Only the server can tell the
    /// two apart, which is what makes this the tier for the claim: a URL that granted write would
    /// let anyone holding it overwrite the object, and nothing below this would notice.
    /// </remarks>
    [SkippableFact]
    public async Task A_read_url_cannot_write()
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        const string Container = Constant.StorageContainer.Media;
        string path = $"integration/{Guid.NewGuid():N}.txt";
        byte[] original = "trail-blaze"u8.ToArray();

        try
        {
            await storage.UploadAsync(
                Container, path, new MemoryStream(original), "text/plain");

            Uri readUrl = await storage.CreateReadUrlAsync(
                Container, path, DateTimeOffset.UtcNow.AddMinutes(5));

            using var http = new HttpClient();

            // A well-formed write: the blob type header is required, and without it the server answers
            // 400 and refuses every write including the ones it should permit. A malformed request
            // would make this test pass on a URL that granted write, which is the failure it exists to
            // catch.
            using var request = new HttpRequestMessage(HttpMethod.Put, readUrl)
            {
                Content = new StringContent("overwritten"),
            };
            request.Headers.Add("x-ms-blob-type", "BlockBlob");

            HttpResponseMessage write = await http.SendAsync(request);

            Assert.False(write.IsSuccessStatusCode, $"The signed URL accepted a write: {write.StatusCode}.");

            // And the refusal was a refusal rather than a write this test did not look at: the bytes
            // are the ones that were uploaded.
            using var buffer = new MemoryStream();
            await fixture.Container(Container).GetBlobClient(path).DownloadToAsync(buffer);

            Assert.Equal(original, buffer.ToArray());
        }
        finally
        {
            await storage.DeleteAsync(Container, path);
        }
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
