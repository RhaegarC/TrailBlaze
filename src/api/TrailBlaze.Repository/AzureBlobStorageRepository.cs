namespace TrailBlaze.Repository;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Model;

/// <summary>
/// The real <see cref="IStorageRepository"/>, over Azure Blob Storage. This is the only type in
/// the solution that names an Azure storage or identity type: every caller reaches storage through
/// <see cref="IStorageRepository"/> and the container constants, so the vendor is confined to
/// this file.
/// </summary>
/// <remarks>
/// <para>
/// <b>It authenticates one of two ways, and which one is the caller's choice.</b> Given an account
/// URL it acts as the application's own identity — the same identity the host already runs as — and
/// signs each read URL with a <b>user delegation key</b> it asks the service for. No account key is
/// configured, and a signature can be revoked at the account. Given a shared-key connection string
/// it signs locally from the key that string carries, which is what the emulator and the local
/// stack run on and needs no tenant.
/// </para>
/// <para>
/// On the identity path the key is a network round trip, so it is fetched once and held for
/// <see cref="DelegationKeyLifetime.Requested"/> — a listing that mints a URL per item makes one
/// call, not one per item. The cache is per instance and in memory, and that is sufficient rather
/// than a compromise: a delegation key belongs to the account and is not caller-dependent, so
/// several replicas each holding their own copy are all saying the same thing.
/// </para>
/// <para>
/// There is no fake of this type anywhere in the solution. The <c>Category=Container</c> tier in
/// <c>TrailBlaze.Repository.Test</c> constructs this class directly and runs it against a live
/// account — the Azurite emulator by default, which needs no credentials, or a real account when
/// <c>TRAILBLAZE_STORAGE_CONNECTION</c> names one. When nothing answers at the endpoint, those
/// tests <b>skip</b> rather than fail.
/// </para>
/// </remarks>
public sealed class AzureBlobStorageRepository : IStorageRepository
{
    private readonly BlobServiceClient _serviceClient;
    private readonly TimeProvider _clock;

    /// <summary>Whether a read URL is signed with a delegation key or with the account key the
    /// connection string carries.</summary>
    private readonly bool _signsAsIdentity;

    /// <summary>The key in hand, replaced when it stops covering a URL this application may sign.
    /// Written only under <see cref="_keyGate"/>; the type is immutable, so a reader outside the
    /// gate either sees the previous key or this one.</summary>
    private UserDelegationKey? _delegationKey;

    /// <summary>One fetch at a time, so several requests arriving together on a cold or stale key
    /// produce one round trip rather than one each.</summary>
    private readonly SemaphoreSlim _keyGate = new(1, 1);

    /// <param name="connectionString">The account's shared key. Resolved by the composition root
    /// from configuration and passed down, so this layer stays configuration-agnostic
    /// (STANDARD §6).</param>
    /// <param name="options">Transport and retry settings. Set only to reach an endpoint whose
    /// certificate is not publicly trusted.</param>
    public AzureBlobStorageRepository(string connectionString, BlobClientOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _serviceClient = new BlobServiceClient(connectionString, options);
        _clock = TimeProvider.System;
        _signsAsIdentity = false;
    }

    /// <summary>The same repository, authenticating as <paramref name="credential"/>'s identity
    /// rather than with an account key.</summary>
    /// <param name="clock">The clock the key's remaining life is measured against, so a test can
    /// outlast a window it cannot wait for. The machine's own when omitted.</param>
    public AzureBlobStorageRepository(
        Uri accountUri,
        TokenCredential credential,
        BlobClientOptions? options = null,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(accountUri);

        _serviceClient = new BlobServiceClient(accountUri, credential, options);
        _clock = clock ?? TimeProvider.System;
        _signsAsIdentity = true;
    }

    /// <summary>The same repository authenticating as the identity this host runs as, which is the
    /// credential a deployment already has and the reason no account key is configured.</summary>
    public static AzureBlobStorageRepository ForAccount(Uri accountUri, BlobClientOptions? options = null) =>
        new(accountUri, new DefaultAzureCredential(), options);

    /// <inheritdoc/>
    public async Task<string> UploadAsync(
        string container,
        string path,
        Stream content,
        string contentType,
        string? cacheControl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        BlobClient blob = GetBlobClient(container, path);

        // Stating the content type is what makes the object serve back as an image rather
        // than as an opaque download, so it travels with the bytes rather than being
        // guessed from the extension at read time.
        var headers = new BlobHttpHeaders { ContentType = contentType };

        // Set only when the caller named one. Storage does not decide that a browser may keep
        // these bytes; it stores the decision the caller made, and an absent header is the
        // caller having made none.
        if (cacheControl is not null)
        {
            headers.CacheControl = cacheControl;
        }

        var options = new BlobUploadOptions { HttpHeaders = headers };

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
    public async Task<Uri> CreateReadUrlAsync(
        string container,
        string path,
        DateTimeOffset expiresOn,
        CancellationToken cancellationToken = default)
    {
        // The default is the value a forgotten argument carries, and it is not an instant anybody
        // means — so it is the one expiry refused here. A past instant is signed, and the backend
        // refuses it on fetch, which is how the storage tier proves an expired SAS does not open.
        if (expiresOn == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresOn), expiresOn, "The read URL needs the instant it expires.");
        }

        BlobClient blob = GetBlobClient(container, path);

        var sas = new BlobSasBuilder
        {
            BlobContainerName = container,
            BlobName = path,
            Resource = "b",

            // Verbatim. Nothing is added to it and nothing is rounded, so the caller's instant is
            // the one the token carries.
            ExpiresOn = expiresOn,
        };
        sas.SetPermissions(BlobSasPermissions.Read);

        if (!_signsAsIdentity)
        {
            // Throws when the client holds no shared key to sign with, which is the intended
            // loud failure rather than a URL that silently carries no authorization.
            return blob.GenerateSasUri(sas);
        }

        // Awaited, and deliberately uncaught: an identity that cannot be authenticated is an
        // error here. Falling back to the blob's own URL would return a string of exactly the
        // right shape that authorizes nothing, and the caller would learn about it from a
        // customer's broken image rather than from this.
        UserDelegationKey key = await DelegationKeyAsync(cancellationToken);

        return new Uri($"{blob.Uri}?{sas.ToSasQueryParameters(key, _serviceClient.AccountName)}");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The blob's own URI with nothing appended. No SAS is built and no key is touched, so this is
    /// the one operation here that works however little the client can authenticate with.
    /// </remarks>
    public Uri CreatePublicUrl(string container, string path) =>
        GetBlobClient(container, path).Uri;

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
        // no SAS of its own -- this client's own credential authorizes it.
        CopyFromUriOperation copy = await destination.StartCopyFromUriAsync(source.Uri, cancellationToken: cancellationToken);
        await copy.WaitForCompletionAsync(cancellationToken);

        await source.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        return destinationPath;
    }

    /// <summary>The key in hand, or another one when there is none or the one held no longer
    /// covers the longest URL this application may sign.</summary>
    private async Task<UserDelegationKey> DelegationKeyAsync(CancellationToken cancellationToken)
    {
        if (Held() is { } key)
        {
            return key;
        }

        await _keyGate.WaitAsync(cancellationToken);

        try
        {
            // Asked again inside the gate: whoever held it may have just fetched the key this
            // caller would otherwise fetch a second time.
            if (Held() is { } fetched)
            {
                return fetched;
            }

            DateTimeOffset now = _clock.GetUtcNow();

            // The token is passed explicitly because the options-taking overload has a defaulted
            // one, so a two-argument call binds to it and an expiry would be read as a token.
            Response<UserDelegationKey> response = await _serviceClient.GetUserDelegationKeyAsync(
                now, now + DelegationKeyLifetime.Requested, cancellationToken);

            _delegationKey = response.Value;
            return _delegationKey;
        }
        finally
        {
            _keyGate.Release();
        }
    }

    /// <summary>The key in hand if it may still sign, and null otherwise — which is what makes a
    /// stale key be fetched again rather than reused.</summary>
    private UserDelegationKey? Held()
    {
        UserDelegationKey? key = _delegationKey;

        return key is not null && DelegationKeyLifetime.ReusableAt(_clock.GetUtcNow(), key.SignedExpiresOn)
            ? key
            : null;
    }

    private BlobClient GetBlobClient(string container, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(container);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return _serviceClient.GetBlobContainerClient(container).GetBlobClient(path);
    }
}
