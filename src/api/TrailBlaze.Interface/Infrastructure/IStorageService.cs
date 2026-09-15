namespace TrailBlaze.Interface.Infrastructure
{
    /// <summary>
    /// Content-addressed storage for the media the app serves: cover images, avatars, and
    /// activity media. It is an abstraction over a bucket, not over a vendor — no member here
    /// names an Azure type, so callers depend on the operation rather than on the SDK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The container is a parameter of every operation</b>, not a property of the service.
    /// The destination is a decision the caller makes — feature 08 routes a cover by the
    /// activity's <c>Type</c>, and avatars go to their own container — and the container name is
    /// the whole of the public/private answer. The closed set lives in
    /// <c>Constant.StorageContainer</c>; pass those, not string literals.
    /// </para>
    /// <para>
    /// <b>Unit tests inject an in-memory fake</b> and therefore prove the caller's logic, never
    /// the vendor implementation. Anything specific to the real backend — SAS generation,
    /// container existence, content-type round-tripping — is only proven by the tagged storage
    /// integration tier. Azure is a real dependency in every environment, including tests.
    /// </para>
    /// </remarks>
    public interface IStorageService
    {
        /// <summary>
        /// Stores <paramref name="content"/> and returns the path it can be read back by.
        /// </summary>
        /// <param name="container">One of <c>Constant.StorageContainer</c>.</param>
        /// <param name="path">Destination path within the container.</param>
        /// <param name="content">The bytes to store.</param>
        /// <param name="contentType">Content type recorded with the blob, so it is served back
        /// correctly rather than as an opaque download.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stored path — what a caller persists and later hands to
        /// <see cref="CreateReadUrlAsync"/> or <see cref="DeleteAsync"/>.</returns>
        Task<string> UploadAsync(
            string container,
            string path,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an object. Deleting something already gone is not an error — the caller asked
        /// for the object to not be there, and it is not.
        /// </summary>
        /// <param name="container">One of <c>Constant.StorageContainer</c>.</param>
        /// <param name="path">Path within the container.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteAsync(
            string container,
            string path,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Mints a read URL for an object.
        /// </summary>
        /// <remarks>
        /// This is the only way a caller reaches an object in a private container, so the
        /// lifetime is required rather than defaulted: a URL with no expiry is a leaked object,
        /// and the caller is the only one who knows how long the link needs to live.
        /// </remarks>
        /// <param name="container">One of <c>Constant.StorageContainer</c>.</param>
        /// <param name="path">Path within the container.</param>
        /// <param name="lifetime">How long the returned URL stays valid. Must be positive.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A URL readable without further authorization until it expires.</returns>
        Task<Uri> CreateReadUrlAsync(
            string container,
            string path,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves an object to another container, returning its new path.
        /// </summary>
        /// <remarks>
        /// A move, not a copy: the source is gone afterwards. The two-container shape is the
        /// point rather than an incidental parameter — feature 08's visibility change moves an
        /// object between the public and private containers, and a caller that had to compose
        /// copy-then-delete itself would be free to get the order wrong and leave the object in
        /// both places, or in neither.
        /// </remarks>
        /// <param name="sourceContainer">Container holding the object now.</param>
        /// <param name="sourcePath">Path within <paramref name="sourceContainer"/>.</param>
        /// <param name="destinationContainer">Container to move it to.</param>
        /// <param name="destinationPath">Path within <paramref name="destinationContainer"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The path within <paramref name="destinationContainer"/>.</returns>
        Task<string> MoveAsync(
            string sourceContainer,
            string sourcePath,
            string destinationContainer,
            string destinationPath,
            CancellationToken cancellationToken = default);
    }
}
