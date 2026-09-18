namespace TrailBlaze.Repository.Test.TestSupport;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TrailBlaze.Model;

/// <summary>
/// A live storage account with the three containers of the closed set provisioned, one
/// collection for the whole tier.
/// </summary>
/// <remarks>
/// <para>
/// <b>One account, not one per class.</b> Containers are shared in production and the objects
/// in them are GUID-keyed, so there is nothing to isolate: two classes writing under distinct
/// paths cannot collide, and a container per class would provision four containers where
/// production has three, which is a shape no deployment has.
/// </para>
/// <para>
/// <b>Where the skip boundary is.</b> Nothing answering at the configured endpoint skips:
/// the emulator is simply not running. Everything past that is a failure. Uploading, minting a
/// signature or reading a blob back are the exact operations this tier exists to check, and
/// degrading one to a skip would hide the thing it was built for.
/// </para>
/// </remarks>
public sealed class AzureStorageFixture : IAsyncLifetime
{
    /// <summary>
    /// The closed set from <see cref="Constant.StorageContainer"/>, listed here rather than
    /// read reflectively. Adding a container to the constant without provisioning it here is
    /// caught by <c>StorageProvisioningTests</c>, which asks the account about each name the
    /// constant declares.
    /// </summary>
    private static readonly string[] Containers =
    [
        Constant.StorageContainer.Covers,
        Constant.StorageContainer.Avatars,
        Constant.StorageContainer.Media,
    ];

    /// <summary>False when no account answers, in which case every test must skip rather than
    /// fail. Read it through <see cref="Create"/>, which calls <c>Skip.IfNot</c>.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Why <see cref="IsAvailable"/> is false. Empty when it is true.</summary>
    public string SkipReason { get; private set; } = string.Empty;

    /// <summary>The account this fixture provisioned, as resolved by
    /// <see cref="TestEnvironment.Storage"/>.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the endpoint is this machine — the emulator — and therefore whether this
    /// fixture was permitted to change a container's access level.
    /// </summary>
    /// <remarks>
    /// This is a safety boundary, not a convenience flag. A test run must never change the
    /// access level of a container in a real account: that is a deployment change with a
    /// security consequence, made silently, by a process whose whole job is to observe. So the
    /// level is set only when the endpoint is loopback, where the account belongs to whoever
    /// started the container and is discarded with it.
    /// </remarks>
    public bool IsEmulator { get; private set; }

    /// <summary>A client for this account, for the assertions that need to address a blob or
    /// a container directly rather than through <c>IStorageRepository</c>.</summary>
    public BlobServiceClient Service => new(ConnectionString);

    public async Task InitializeAsync()
    {
        string connection = TestEnvironment.Storage();

        if (!await AnswersAsync(connection))
        {
            SkipReason = UnreachableReason(connection);
            return;
        }

        ConnectionString = connection;
        IsEmulator = new BlobServiceClient(connection).Uri.IsLoopback;

        foreach (string container in Containers)
        {
            BlobContainerClient client = Service.GetBlobContainerClient(container);

            // The application does not provision containers -- that is a deployment concern --
            // so the tier that needs them creates them, exactly as the integration test did
            // for `media` alone.
            await client.CreateIfNotExistsAsync();

            if (IsEmulator)
            {
                // Set rather than assumed, and for `media` set to None explicitly: the
                // private-by-default state is then asserted by the tests below instead of
                // being whatever the emulator happens to do. Real Azure is left alone.
                await client.SetAccessPolicyAsync(AccessLevelFor(container));
            }
        }

        IsAvailable = true;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// A client for one container, which the caller disposes nothing of.
    /// </summary>
    /// <remarks>
    /// Skips rather than throwing when the account is absent, so a test cannot forget to check
    /// availability and fail with a connection error that says nothing about the container.
    /// The calling test must be <c>[SkippableFact]</c> — under a plain <c>[Fact]</c> the
    /// <c>SkipException</c> is just an exception and the test fails.
    /// </remarks>
    public BlobContainerClient Container(string name)
    {
        Skip.IfNot(IsAvailable, SkipReason);

        return Service.GetBlobContainerClient(name);
    }

    /// <summary>
    /// The production implementation against this account, with the same skip contract as
    /// <see cref="Container"/>.
    /// </summary>
    public AzureBlobStorageRepository Repository()
    {
        Skip.IfNot(IsAvailable, SkipReason);

        return new AzureBlobStorageRepository(ConnectionString);
    }

    /// <summary>
    /// Whether anything answers at the given string.
    /// </summary>
    /// <remarks>
    /// Built without retries on purpose: the default policy retries a refused connection
    /// several times with backoff, which turns "the emulator is not running" from an instant
    /// skip into a stall in every storage class on a machine that has no container.
    /// </remarks>
    private static async Task<bool> AnswersAsync(string connectionString)
    {
        var options = new BlobClientOptions
        {
            Retry = { MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(5) },
        };

        try
        {
            await new BlobServiceClient(connectionString, options).GetPropertiesAsync();
            return true;
        }
        catch (Exception exception) when (exception is RequestFailedException or HttpRequestException)
        {
            return false;
        }
    }

    private static string UnreachableReason(string connectionString)
    {
        string endpoint = new BlobServiceClient(connectionString).Uri.ToString();

        return TestEnvironment.StorageIsConfigured
            ? $"{TestEnvironment.StorageConnectionVariable} points at {endpoint}, and nothing "
              + "answered there."
            : $"Nothing answered at {endpoint}, which is the Azurite emulator this tier falls "
              + "back to. Start it with `docker compose -f docker-compose.test.yml up -d` from "
              + "src/api and `docker compose -f docker-compose.test.yml ps` to check.";
    }

    /// <summary>
    /// The access level each container is meant to have in production.
    /// </summary>
    /// <remarks>
    /// This is the one executable statement of a set that otherwise exists only in
    /// <see cref="Constant.StorageContainer"/>'s XML comments — and prose does not configure
    /// anything. Until the deployment sets these levels, <c>CreatePublicUrl</c> returns a URL
    /// that 404s. Recorded in the debt register rather than fixed here: setting a container's
    /// access level is a deployment change and this is a test change.
    /// </remarks>
    private static PublicAccessType AccessLevelFor(string container) =>
        container == Constant.StorageContainer.Media
            ? PublicAccessType.None
            : PublicAccessType.Blob;
}
