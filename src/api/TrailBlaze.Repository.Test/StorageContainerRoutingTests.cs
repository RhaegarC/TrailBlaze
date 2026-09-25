namespace TrailBlaze.Repository.Test;

using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TrailBlaze.Model;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The container is the whole of the public/private answer, proven against a live account
/// rather than against a dictionary.
/// </summary>
/// <remarks>
/// <para>
/// These were <c>StorageSeamTests</c>, which asserted call order and arguments through an
/// in-memory fake. The fake is gone, and this is not a rewording of what it said: a real
/// backend records nothing, so "the caller passed <c>Covers</c>" is not observable here and
/// never could be. What replaces it is the outcome that made the argument matter — the object
/// is readable by an unsigned URL and lives in the container that serves it.
/// </para>
/// <para>
/// <b>One claim is genuinely lost and is named rather than dropped.</b> The old tier could see
/// the container each call <em>asked for</em>, so it could catch a caller that moved the wrong
/// object to the right place. Here the object arrives and the source empties, and which
/// arguments produced that is invisible. Feature 08's routing is a decision taken in the
/// service layer and will need a recording double of its own to assert it — see
/// <c>docs/features/08-*.md</c>.
/// </para>
/// </remarks>
[Trait("Category", "Container")]
public sealed class StorageContainerRoutingTests(AzureStorageFixture fixture)
    : IClassFixture<AzureStorageFixture>
{
    /// <summary>
    /// A move relocates the object: it arrives carrying its bytes and the source is gone.
    /// </summary>
    /// <remarks>
    /// The real implementation copies, waits, then deletes, so this exercises a server-side
    /// copy — a call that has to complete before the source is removed and that, if it never
    /// reported success, would leave the object in one container or neither.
    /// </remarks>
    [SkippableFact]
    public async Task A_move_relocates_the_object_and_removes_the_source()
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        byte[] content = "move-me"u8.ToArray();

        string source = Unique("move");
        string destination = Unique("move");

        try
        {
            await storage.UploadAsync(
                Constant.StorageContainer.Media, source, new MemoryStream(content),
                "application/octet-stream");

            string moved = await storage.MoveAsync(
                Constant.StorageContainer.Media, source,
                Constant.StorageContainer.Covers, destination);

            Assert.Equal(destination, moved);
            Assert.False(await ExistsAsync(Constant.StorageContainer.Media, source));
            Assert.Equal(
                content,
                await DownloadAsync(Constant.StorageContainer.Covers, destination));
        }
        finally
        {
            await CleanUpAsync(storage, source, destination);
        }
    }

    /// <summary>
    /// The read URL of a private container is signed and expires when it was asked to.
    /// </summary>
    /// <remarks>
    /// The expiry is the load-bearing half. A URL that is signed but never expires is a
    /// leaked object, and a URL that expires on some default rather than on the instant the
    /// caller named silently breaks the promise the caller made to whoever it handed the link to.
    /// The comparison is exact rather than a window, because the caller names the instant and the
    /// signature carries it to the second: anything else would leave the caller reporting a moment
    /// the token does not hold.
    /// </remarks>
    [SkippableFact]
    public async Task A_read_url_is_signed_and_expires_when_it_was_told_to()
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        string path = Unique("sas");

        try
        {
            await storage.UploadAsync(
                Constant.StorageContainer.Media, path, new MemoryStream("signed"u8.ToArray()),
                "text/plain");

            DateTimeOffset expiresOn = new SignedUrlLifetime(TimeSpan.FromMinutes(5))
                .ExpiryFrom(DateTimeOffset.UtcNow);

            Uri url = await storage.CreateReadUrlAsync(
                Constant.StorageContainer.Media, path, expiresOn);

            Assert.Contains("sig=", url.Query);
            Assert.Equal(expiresOn, SignedUrl.ExpiryOf(url));
        }
        finally
        {
            await storage.DeleteAsync(Constant.StorageContainer.Media, path);
        }
    }

    /// <summary>
    /// The signature grants a read of one blob and nothing else.
    /// </summary>
    /// <remarks>
    /// Both halves are load-bearing and neither is visible from the call: a SAS that granted write
    /// would let anyone holding the URL overwrite the object, and one scoped to the container would
    /// open every other item in it. The claim is read off the URL the caller is handed rather than
    /// from the arguments that built it, because a permission set that never reached the builder
    /// still produces a well-formed URL. That the server <em>honours</em> the scope — a write is
    /// refused — is the half this cannot make, and
    /// <see cref="AzureBlobStorageIntegrationTests"/> makes it.
    /// </remarks>
    [SkippableFact]
    public async Task A_read_url_grants_a_read_of_one_blob_and_no_more()
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        string path = Unique("scope");

        try
        {
            await storage.UploadAsync(
                Constant.StorageContainer.Media, path, new MemoryStream("scoped"u8.ToArray()),
                "text/plain");

            Uri url = await storage.CreateReadUrlAsync(
                Constant.StorageContainer.Media,
                path,
                new SignedUrlLifetime(TimeSpan.FromMinutes(5)).ExpiryFrom(DateTimeOffset.UtcNow));

            Assert.Equal("r", SignedUrl.PermissionsOf(url));

            // `b` for blob: `c` would be a container-wide signature, which the API never mints.
            Assert.Equal("b", SignedUrl.ResourceOf(url));

            // And the path names this object, so a signature scoped to a blob other than the one the
            // caller asked for would not read back.
            Assert.EndsWith($"/{Constant.StorageContainer.Media}/{path}", url.AbsolutePath);
        }
        finally
        {
            await storage.DeleteAsync(Constant.StorageContainer.Media, path);
        }
    }

    /// <summary>
    /// An object in a public container is readable by a URL carrying nothing.
    /// </summary>
    /// <remarks>
    /// This is PRD Decision #28 asserted for the first time, and it is exactly the pair feature
    /// 07 forbids mixing: the container grants the read, so attaching a SAS would grant nothing
    /// the container had not already granted while turning a link that was always public into
    /// one that dies. The URL is checked for the absence of a signature as well as for
    /// reachability, because a signed URL would also fetch — and would be the bug.
    /// </remarks>
    [SkippableTheory]
    [InlineData(Constant.StorageContainer.Covers, "image/jpeg")]
    [InlineData(Constant.StorageContainer.Avatars, "image/png")]
    public async Task A_public_container_serves_its_object_to_an_unsigned_url(
        string container, string contentType)
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        string path = Unique("public");
        byte[] content = "public-bytes"u8.ToArray();

        try
        {
            await storage.UploadAsync(container, path, new MemoryStream(content), contentType);

            Uri publicUrl = storage.CreatePublicUrl(container, path);
            Assert.DoesNotContain("sig=", publicUrl.Query);

            using var http = new HttpClient();
            HttpResponseMessage response = await http.GetAsync(publicUrl);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(content, await response.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            await storage.DeleteAsync(container, path);
        }
    }

    /// <summary>
    /// The negative control for the test above: the same URL shape against the private
    /// container does not serve the object.
    /// </summary>
    /// <remarks>
    /// Without this, "an unsigned URL works" would pass just as well if the account made
    /// everything public — the test would be describing the account rather than the container.
    /// The status is asserted as "not 200" rather than pinned, because real Azure answers 404
    /// for an anonymous read of a private blob and Azurite has historically answered 403, and
    /// which one arrives is a vendor detail this tier has no business fixing.
    /// </remarks>
    [SkippableFact]
    public async Task A_private_container_does_not_serve_its_object_to_an_unsigned_url()
    {
        AzureBlobStorageRepository storage = fixture.Repository();
        string path = Unique("private");

        try
        {
            await storage.UploadAsync(
                Constant.StorageContainer.Media, path, new MemoryStream("private"u8.ToArray()),
                "text/plain");

            Uri unsigned = storage.CreatePublicUrl(Constant.StorageContainer.Media, path);

            using var http = new HttpClient();
            HttpResponseMessage response = await http.GetAsync(unsigned);

            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await storage.DeleteAsync(Constant.StorageContainer.Media, path);
        }
    }

    /// <summary>
    /// The account agrees with the prose: <c>media</c> is private and the other two are public.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Until now this set existed only in <see cref="Constant.StorageContainer"/>'s XML
    /// comments. Prose configures nothing, so <c>CreatePublicUrl</c> returned a URL that 404s
    /// on a deployment where nobody had set the level, and no artefact said who was supposed to.
    /// <see cref="AzureStorageFixture"/> sets it on the emulator, and this reads it back, which
    /// makes the set executable in at least one place.
    /// </para>
    /// <para>
    /// Only meaningful where the fixture set the level, which is loopback only. On a real
    /// account this skips rather than asserting an operator's deployment decision — a test run
    /// must not read a real container's access level as a requirement, and must certainly not
    /// write one.
    /// </para>
    /// </remarks>
    [SkippableFact]
    public async Task The_media_container_is_private_and_the_other_two_are_public()
    {
        Skip.IfNot(
            fixture.IsEmulator,
            "Container access levels are set only on the emulator; on a real account they are a "
            + "deployment decision this tier does not assert.");

        Assert.Equal(
            PublicAccessType.None,
            (await fixture.Container(Constant.StorageContainer.Media).GetPropertiesAsync())
                .Value.PublicAccess);

        foreach (string container in
            new[] { Constant.StorageContainer.Covers, Constant.StorageContainer.Avatars })
        {
            Assert.Equal(
                PublicAccessType.Blob,
                (await fixture.Container(container).GetPropertiesAsync()).Value.PublicAccess);
        }
    }

    /// <summary>
    /// Every container the closed set declares answers on the account.
    /// </summary>
    /// <remarks>
    /// The set is a constant and the provisioning is a deployment concern, so nothing else
    /// connects the two: a container added to <c>Constant.StorageContainer</c> and forgotten
    /// here would surface as a 404 from whichever feature used it first, at runtime, in
    /// production.
    /// </remarks>
    [SkippableFact]
    public async Task Every_container_the_closed_set_declares_exists_on_the_account()
    {
        foreach (string container in
            new[]
            {
                Constant.StorageContainer.Covers,
                Constant.StorageContainer.Avatars,
                Constant.StorageContainer.Media,
            })
        {
            Assert.True(
                await fixture.Container(container).ExistsAsync(),
                $"`{container}` is declared by Constant.StorageContainer but does not exist on "
                + "the account.");
        }
    }

    /// <summary>
    /// A read URL with no expiry is a leaked object, so an expiry that was never named is
    /// refused rather than defaulted to something.
    /// </summary>
    /// <remarks>
    /// The guard is on <c>default</c> rather than on a duration, because the caller names the
    /// instant: there is no duration here to be non-positive, and the one value that cannot mean
    /// an instant is the one with no ticks in it. The wider policy — that a configured window is
    /// clamped and a nonsensical one falls back — lives in <c>SignedUrlLifetime</c> and is unit
    /// tested there, so this stays the repository's own single claim.
    /// </remarks>
    [SkippableFact]
    public async Task A_read_url_needs_the_instant_it_expires()
    {
        AzureBlobStorageRepository storage = fixture.Repository();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => storage.CreateReadUrlAsync(
                Constant.StorageContainer.Media,
                Unique("never-written"),
                default));
    }

    /// <summary>A path unique to this run, so a failure cannot be read as the last one's
    /// leftovers and a cleanup can never remove another test's object.</summary>
    private static string Unique(string prefix) => $"{prefix}/{Guid.NewGuid():N}";

    private async Task<bool> ExistsAsync(string container, string path) =>
        await fixture.Container(container).GetBlobClient(path).ExistsAsync();

    private async Task<byte[]> DownloadAsync(string container, string path)
    {
        using var buffer = new MemoryStream();
        await fixture.Container(container).GetBlobClient(path).DownloadToAsync(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    /// Removes whatever this test left, whichever container it is in, without failing the test
    /// if the cleanup is what went wrong.
    /// </summary>
    /// <remarks>
    /// The containers outlive the run, so an object left behind is not merely untidy on a
    /// configured real account — the emulator is discarded with its container, but a real one
    /// is not. Deleting an absent object is already not an error, so this needs no existence
    /// check of its own.
    /// </remarks>
    private async Task CleanUpAsync(
        AzureBlobStorageRepository storage, params string[] paths)
    {
        foreach (string path in paths)
        {
            foreach (string container in
                new[]
                {
                    Constant.StorageContainer.Media,
                    Constant.StorageContainer.Covers,
                    Constant.StorageContainer.Avatars,
                })
            {
                await storage.DeleteAsync(container, path);
            }
        }
    }
}
