namespace TrailBlaze.Repository.Test;

using System.Net;
using TrailBlaze.Model;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The visibility change as a real round trip, against a live account rather than a double.
/// </summary>
/// <remarks>
/// <para>
/// This is the tier that cannot be replaced, and the reason is the whole of the rule. A recording
/// double can prove <c>MoveAsync</c> was <em>called</em> with the right pair of containers — that is
/// asserted in <c>TrailBlaze.Service.Test</c>, and it is the only thing a double can say. It cannot
/// say the bytes left the public container, and "the bytes left the public container" is the
/// property the feature exists to guarantee: a public URL cannot be recalled, so a cover left behind
/// by a <c>Public</c> → <c>Private</c> edit stays fetchable by anyone who ever held the link.
/// </para>
/// <para>
/// <b>The assertion is therefore on fetch, not on the call.</b> A credential-free HTTP GET against
/// the URL the old cover was served by has to stop working, and a freshly minted SAS against the new
/// location has to work — both directions, because "the old URL fails" would also pass if the move
/// had destroyed the object outright.
/// </para>
/// </remarks>
[Trait("Category", "Container")]
public sealed class CoverVisibilityMoveTests(AzureStorageFixture fixture)
    : IClassFixture<AzureStorageFixture>
{
    private static readonly byte[] Cover = "cover-bytes"u8.ToArray();

    /// <summary>
    /// `Public` → `Shared`/`Private`: the copy in the public container goes, and the only way left to
    /// the image is a signed one.
    /// </summary>
    [SkippableFact]
    public async Task Leaving_the_public_container_stops_the_old_url_serving()
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        string path = Unique("cover");

        try
        {
            await storage.UploadAsync(
                Constant.StorageContainer.Covers, path, new MemoryStream(Cover), "image/jpeg");

            Uri publicUrl = storage.CreatePublicUrl(Constant.StorageContainer.Covers, path);

            // The precondition, asserted rather than assumed: without it, "the URL stopped working"
            // would pass just as well on an account where the covers container was never public.
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(publicUrl));

            await storage.MoveAsync(
                Constant.StorageContainer.Covers, path,
                Constant.StorageContainer.Media, path);

            Assert.NotEqual(HttpStatusCode.OK, await StatusAsync(publicUrl));

            // And the image is still there, reached the one way a private object can be — so the
            // assertion above says the bytes moved rather than that they were lost.
            Uri signed = await storage.CreateReadUrlAsync(
                Constant.StorageContainer.Media, path, Constant.CoverUrl.SasLifetime);

            Assert.Equal(HttpStatusCode.OK, await StatusAsync(signed));
        }
        finally
        {
            await CleanUpAsync(storage, path);
        }
    }

    /// <summary>
    /// The reverse direction, and it is the other half of the same rule: a cover promoted to
    /// `Public` must be reachable by a plain URL, which a move that left the bytes in the private
    /// container would not achieve — a well-formed public URL for a private blob 404s.
    /// </summary>
    [SkippableFact]
    public async Task Arriving_in_the_public_container_serves_the_bytes_unsigned()
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        string path = Unique("cover");

        try
        {
            await storage.UploadAsync(
                Constant.StorageContainer.Media, path, new MemoryStream(Cover), "image/jpeg");

            await storage.MoveAsync(
                Constant.StorageContainer.Media, path,
                Constant.StorageContainer.Covers, path);

            Uri publicUrl = storage.CreatePublicUrl(Constant.StorageContainer.Covers, path);

            Assert.DoesNotContain("sig=", publicUrl.Query);

            using var http = new HttpClient();
            HttpResponseMessage response = await http.GetAsync(publicUrl);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(Cover, await response.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            await CleanUpAsync(storage, path);
        }
    }

    /// <summary>
    /// A cover that is replaced leaves no readable copy behind: after the old object is deleted, the
    /// URL it was served by is gone.
    /// </summary>
    /// <remarks>
    /// The replace path deletes the previous blob, and this is what makes the deletion mean
    /// something. It is the same failure the move is guarded against, reached by the other route:
    /// an orphaned public cover is a picture from an entry nobody may read, still fetchable.
    /// </remarks>
    [SkippableFact]
    public async Task A_replaced_cover_leaves_no_readable_copy()
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        string replaced = Unique("cover");
        string replacement = Unique("cover");

        try
        {
            await storage.UploadAsync(
                Constant.StorageContainer.Covers, replaced, new MemoryStream(Cover), "image/jpeg");

            Uri oldUrl = storage.CreatePublicUrl(Constant.StorageContainer.Covers, replaced);
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(oldUrl));

            await storage.UploadAsync(
                Constant.StorageContainer.Covers, replacement,
                new MemoryStream("newer"u8.ToArray()), "image/jpeg");

            await storage.DeleteAsync(Constant.StorageContainer.Covers, replaced);

            Assert.NotEqual(HttpStatusCode.OK, await StatusAsync(oldUrl));
        }
        finally
        {
            await CleanUpAsync(storage, replaced, replacement);
        }
    }

    private static async Task<HttpStatusCode> StatusAsync(Uri url)
    {
        using var http = new HttpClient();

        return (await http.GetAsync(url)).StatusCode;
    }

    /// <summary>A path unique to this run, so a failure cannot be read as the last one's leftovers
    /// and a cleanup can never remove another test's object.</summary>
    private static string Unique(string prefix) => $"{prefix}/{Guid.NewGuid():N}";

    /// <summary>
    /// Removes whatever this test left, whichever container the move put it in, without failing the
    /// test if the cleanup is what went wrong. The emulator is discarded with its container; a real
    /// account is not, and an object left behind there is not merely untidy.
    /// </summary>
    private async Task CleanUpAsync(AzureBlobStorageRepository storage, params string[] paths)
    {
        foreach (string path in paths)
        {
            await storage.DeleteAsync(Constant.StorageContainer.Media, path);
            await storage.DeleteAsync(Constant.StorageContainer.Covers, path);
        }
    }
}
