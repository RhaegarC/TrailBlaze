namespace TrailBlaze.Repository.Test;

using System.Net;
using Azure.Core;
using TrailBlaze.Model;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The storage credential this feature moves: the application signs and reads as <b>itself</b>
/// rather than with an account key.
/// </summary>
/// <remarks>
/// <para>
/// Every claim here is one no other tier can make. A delegation signature is built from a key the
/// service hands out, so a wrong key, a wrong window or a permission the builder never carried
/// produces a perfectly well-formed URL that the account refuses — invisible until something
/// fetches, which is what this does. And the key's own cost is a network call, so "the listing
/// fetches one key" is a fact about a live endpoint rather than about the code that decides it.
/// </para>
/// <para>
/// The endpoint is the emulator, whose certificate this tier generates and whose OAuth support is
/// what makes the whole feature reachable without a tenant. See the guide on running it.
/// </para>
/// </remarks>
[Trait("Category", "Container")]
public sealed class UserDelegationSigningTests(AzureStorageFixture fixture)
    : IClassFixture<AzureStorageFixture>
{
    /// <summary>
    /// A URL the application signed as itself fetches a private object.
    /// </summary>
    /// <remarks>
    /// The object is uploaded through the same identity, so this is the whole path — write, sign,
    /// read — under a credential that carries no account key. The container is private, so a URL
    /// that authorized nothing would come back refused rather than merely unproven: an unsigned
    /// read of the same blob is asserted to fail by its neighbour.
    /// </remarks>
    [SkippableFact]
    public async Task A_url_the_application_signed_as_itself_reads_a_private_object()
    {
        AzureBlobStorageRepository storage = fixture.SigningRepository(fixture.Identity);
        const string Container = Constant.StorageContainer.Media;
        string path = $"delegation/{Guid.NewGuid():N}.txt";
        byte[] content = "signed by the application"u8.ToArray();

        try
        {
            await storage.UploadAsync(Container, path, new MemoryStream(content), "text/plain");

            Uri readUrl = await storage.CreateReadUrlAsync(
                Container, path, DateTimeOffset.UtcNow.AddMinutes(5));

            using HttpClient http = LoopbackTransport.Client();
            HttpResponseMessage response = await http.GetAsync(readUrl);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(content, await response.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            await storage.DeleteAsync(Container, path);
        }
    }

    /// <summary>
    /// The same URL carries no authorization of its own, so the read above is a statement about the
    /// signature rather than about a container anyone may read.
    /// </summary>
    /// <remarks>
    /// Without this the test above passes on a container that was accidentally public, which is the
    /// failure it exists to catch — an unsigned read is the only thing that tells the two apart.
    /// </remarks>
    [SkippableFact]
    public async Task The_same_object_reads_nothing_without_the_signature()
    {
        AzureBlobStorageRepository storage = fixture.SigningRepository(fixture.Identity);
        const string Container = Constant.StorageContainer.Media;
        string path = $"delegation/{Guid.NewGuid():N}.txt";

        try
        {
            await storage.UploadAsync(
                Container, path, new MemoryStream("signed by the application"u8.ToArray()), "text/plain");

            using HttpClient http = LoopbackTransport.Client();
            HttpResponseMessage response = await http.GetAsync(
                fixture.Container(Container).GetBlobClient(path).Uri);

            Assert.False(response.IsSuccessStatusCode);
        }
        finally
        {
            await storage.DeleteAsync(Container, path);
        }
    }

    /// <summary>
    /// Uploading and deleting work under the identity too, so the whole credential moved rather than
    /// only the signing half.
    /// </summary>
    /// <remarks>
    /// A feature that swapped the key for an identity in the mint alone would leave the data plane on
    /// the account key, and the deployment would still carry the secret this change exists to remove.
    /// Existence is read back through the fixture's own client, so what is asserted is that the
    /// identity's write reached the account rather than that the object appears in a local handle.
    /// </remarks>
    [SkippableFact]
    public async Task Uploading_and_deleting_work_under_the_identity()
    {
        AzureBlobStorageRepository storage = fixture.SigningRepository(fixture.Identity);
        const string Container = Constant.StorageContainer.Media;
        string path = $"delegation/{Guid.NewGuid():N}.txt";

        try
        {
            await storage.UploadAsync(
                Container, path, new MemoryStream("written as the application"u8.ToArray()), "text/plain");

            Assert.True((await fixture.Container(Container).GetBlobClient(path).ExistsAsync()).Value);

            await storage.DeleteAsync(Container, path);

            Assert.False((await fixture.Container(Container).GetBlobClient(path).ExistsAsync()).Value);
        }
        finally
        {
            await fixture.Container(Container).GetBlobClient(path).DeleteIfExistsAsync();
        }
    }

    /// <summary>
    /// A listing that mints one URL per item fetches <b>one</b> delegation key, not one per URL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what separates a cache from a round trip per mint, and the reason the feature is
    /// separate from the URL-stability work it sits beside: signing is local once the key is held, so
    /// N URLs cost one fetch plus N HMACs, and without the cache they cost N fetches.
    /// </para>
    /// <para>
    /// The count is taken on the wire rather than at the credential, because the credential is the
    /// wrong instrument: the pipeline caches the token it was handed, so a counting
    /// <c>TokenCredential</c> reports one whether the key is cached or not — and reports one for the
    /// upload below too. See <see cref="CountingTransport"/>. The write is asserted to cost no key at
    /// all, which is what makes the five mints' single fetch the whole of the claim.
    /// </para>
    /// </remarks>
    [SkippableFact]
    public async Task Many_urls_are_signed_from_one_delegation_key()
    {
        var wire = new CountingTransport(LoopbackTransport.Handler());
        AzureBlobStorageRepository storage = fixture.SigningRepository(
            fixture.Identity, options: LoopbackTransport.Options(wire));
        const string Container = Constant.StorageContainer.Media;
        string path = $"delegation/{Guid.NewGuid():N}.txt";

        try
        {
            await storage.UploadAsync(
                Container, path, new MemoryStream("trail-blaze"u8.ToArray()), "text/plain");

            int afterUpload = wire.DelegationKeyRequests;

            for (int item = 0; item < 5; item++)
            {
                await storage.CreateReadUrlAsync(Container, path, DateTimeOffset.UtcNow.AddMinutes(5));
            }

            Assert.Equal(0, afterUpload);
            Assert.Equal(1, wire.DelegationKeyRequests);
        }
        finally
        {
            await storage.DeleteAsync(Container, path);
        }
    }

    /// <summary>
    /// A key that no longer covers the longest URL the application may sign is fetched again, and
    /// the URL minted from the new one works.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The claim is "never reused", so it has to be asserted in both directions: the first key is
    /// abandoned, and what replaces it is real. A key is fetched with a two-hour window and held
    /// only while an hour of it remains, so a clock advanced past that point stands in for the
    /// hour-and-a-bit of wall time the case would otherwise cost.
    /// </para>
    /// <para>
    /// <b>The emulator cannot be relied on to catch this.</b> A URL whose expiry sits past the
    /// key's own is refused by a real account and accepted by Azurite, so against this endpoint a
    /// reused stale key produces a URL that works — which is exactly why the rule is asserted here
    /// on the fetch rather than on the read.
    /// </para>
    /// <para>
    /// <b>The clock starts two hours back, and has to.</b> A key is requested from this clock but
    /// validated by the service against its own, so a test clock set to the real present would ask
    /// for a window whose last minute is one minute away — and after the advance, for one that does
    /// not open until an hour from now, which the service refuses outright (403, observed). Setting
    /// it back so both keys straddle the real present is what lets the read below be a real check on
    /// the refetched key rather than a check on the test's arithmetic.
    /// </para>
    /// </remarks>
    [SkippableFact]
    public async Task A_key_past_its_reserve_is_fetched_again()
    {
        var wire = new CountingTransport(LoopbackTransport.Handler());
        var clock = new TestClock(DateTimeOffset.UtcNow - DelegationKeyLifetime.Requested
            + TimeSpan.FromMinutes(5));
        AzureBlobStorageRepository storage = fixture.SigningRepository(
            fixture.Identity, clock, LoopbackTransport.Options(wire));
        const string Container = Constant.StorageContainer.Media;
        string path = $"delegation/{Guid.NewGuid():N}.txt";

        try
        {
            await storage.UploadAsync(
                Container, path, new MemoryStream("trail-blaze"u8.ToArray()), "text/plain");

            await storage.CreateReadUrlAsync(Container, path, DateTimeOffset.UtcNow.AddMinutes(5));
            Assert.Equal(1, wire.DelegationKeyRequests);

            // Past the reserve but inside the key: the key is still alive, and still too short to
            // cover a URL this application may sign -- so it is not the one used next.
            clock.Advance(
                DelegationKeyLifetime.Requested - DelegationKeyLifetime.Reserve + TimeSpan.FromMinutes(1));

            Uri second = await storage.CreateReadUrlAsync(
                Container, path, DateTimeOffset.UtcNow.AddMinutes(5));
            Assert.Equal(2, wire.DelegationKeyRequests);

            using HttpClient http = LoopbackTransport.Client();
            HttpResponseMessage response = await http.GetAsync(second);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // The refetch is per key rather than per mint: the mint from the second key reuses it.
            await storage.CreateReadUrlAsync(Container, path, DateTimeOffset.UtcNow.AddMinutes(5));
            Assert.Equal(2, wire.DelegationKeyRequests);
        }
        finally
        {
            await storage.DeleteAsync(Container, path);
        }
    }

    /// <summary>
    /// An identity that cannot sign is an error, never a URL that carries no authorization.
    /// </summary>
    /// <remarks>
    /// The loud failure the account key gave before it — and the reason the mint is not written as
    /// "ask for a key, and if that fails, return the blob's own URL". That fallback would hand back
    /// a string of exactly the right shape, the caller would store it and serve it, and the failure
    /// would surface as a broken image rather than as an error anyone can read.
    /// </remarks>
    [SkippableFact]
    public async Task An_identity_that_cannot_sign_is_an_error_rather_than_an_unsigned_url()
    {
        AzureBlobStorageRepository storage = fixture.SigningRepository(new RefusingCredential());
        const string Container = Constant.StorageContainer.Media;

        await Assert.ThrowsAnyAsync<Exception>(() => storage.CreateReadUrlAsync(
            Container, $"delegation/{Guid.NewGuid():N}.txt", DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}
