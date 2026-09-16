namespace TrailBlaze.Service.Test.TestSupport;

using TrailBlaze.Interface.Repository;

/// <summary>Which operation a recorded call was.</summary>
public enum StorageOperation
{
    Upload,
    Delete,
    CreateReadUrl,
    CreatePublicUrl,
    Move,
}

/// <summary>
/// One call the code under test made, as the fake saw it.
/// </summary>
/// <param name="Operation">What was asked for.</param>
/// <param name="Container">The container the operation addressed. This is the field the
/// later features assert on.</param>
/// <param name="Path">Path within <paramref name="Container"/>.</param>
/// <param name="DestinationContainer">The container a move went to, when it was a move.</param>
/// <param name="DestinationPath">The path a move went to, when it was a move.</param>
/// <param name="Lifetime">The lifetime a read URL was asked for, when it was a read URL.</param>
public sealed record StorageCall(
    StorageOperation Operation,
    string Container,
    string Path,
    string? DestinationContainer = null,
    string? DestinationPath = null,
    TimeSpan? Lifetime = null);

/// <summary>
/// An in-memory <see cref="IStorageRepository"/> for unit tests, so the RED → GREEN loop stays
/// instant and offline (PRD Decision #6).
/// </summary>
/// <remarks>
/// <para>
/// <b>The recorded container is the point.</b> Features 02, 06 and 08 all turn on routing an
/// object to the correct container — avatars to their own, a cover by the activity's type, a
/// visibility change moving something between public and private — and those are decisions
/// taken in the service layer. Recording which container each call asked for is what lets a
/// unit test assert "the right container" without reaching Azure.
/// </para>
/// <para>
/// This proves the caller's logic, never the vendor's. SAS generation, container existence
/// and content-type round-tripping are the real implementation's problems and belong to the
/// tagged storage integration tier — a test that never leaves this fake cannot speak to them.
/// </para>
/// </remarks>
public sealed class FakeStorageRepository : IStorageRepository
{
    private readonly Dictionary<(string Container, string Path), byte[]> _objects = [];
    private readonly List<StorageCall> _calls = [];

    /// <summary>Every call in order.</summary>
    public IReadOnlyList<StorageCall> Calls => _calls;

    /// <summary>The calls that addressed one container, which is how a test asserts routing
    /// without caring what else happened. A move counts under <em>either</em> end: the whole
    /// point of feature 08's routing is that something arrived in a given container, and
    /// matching only the source would report a bug where there is none.</summary>
    public IReadOnlyList<StorageCall> CallsTo(string container) =>
        _calls.Where(call => call.Container == container || call.DestinationContainer == container)
            .ToList();

    /// <summary>The one call made. Fails if the code under test made more or fewer, which is
    /// what catches a caller that uploaded twice instead of moving.</summary>
    public StorageCall SingleCall() => Assert.Single(_calls);

    /// <summary>The one call of a given kind. Used where a test's own setup is itself a
    /// recorded call — an upload followed by a move, say — so the assertion lands on the
    /// operation under test rather than on everything that happened.</summary>
    public StorageCall SingleCallOf(StorageOperation operation) =>
        Assert.Single(_calls, call => call.Operation == operation);

    /// <summary>The bytes held for an object, or null when it is not there.</summary>
    public byte[]? Bytes(string container, string path) =>
        _objects.TryGetValue((container, path), out byte[]? bytes) ? bytes : null;

    public bool Exists(string container, string path) => _objects.ContainsKey((container, path));

    /// <inheritdoc/>
    public Task<string> UploadAsync(
        string container,
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);

        _objects[(container, path)] = buffer.ToArray();
        _calls.Add(new StorageCall(StorageOperation.Upload, container, path));

        return Task.FromResult(path);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(
        string container,
        string path,
        CancellationToken cancellationToken = default)
    {
        // Matches the real implementation's contract: removing something already gone is
        // not an error.
        _objects.Remove((container, path));
        _calls.Add(new StorageCall(StorageOperation.Delete, container, path));

        return Task.CompletedTask;
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

        _calls.Add(new StorageCall(StorageOperation.CreateReadUrl, container, path, Lifetime: lifetime));

        // Deliberately not a real URL and deliberately not reachable: a unit test that
        // followed this would be making a network call, which this tier must never do.
        return Task.FromResult(new Uri($"fake://{container}/{path}"));
    }

    /// <inheritdoc/>
    public Uri CreatePublicUrl(string container, string path)
    {
        _calls.Add(new StorageCall(StorageOperation.CreatePublicUrl, container, path));

        // No lifetime to carry and nothing signed, which is the difference this call exists
        // to record: a test can assert the public path was taken rather than the SAS one.
        return new Uri($"fake-public://{container}/{path}");
    }

    /// <inheritdoc/>
    public Task<string> MoveAsync(
        string sourceContainer,
        string sourcePath,
        string destinationContainer,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (_objects.Remove((sourceContainer, sourcePath), out byte[]? bytes))
        {
            _objects[(destinationContainer, destinationPath)] = bytes;
        }

        _calls.Add(new StorageCall(
            StorageOperation.Move,
            sourceContainer,
            sourcePath,
            DestinationContainer: destinationContainer,
            DestinationPath: destinationPath));

        return Task.FromResult(destinationPath);
    }
}
