namespace TrailBlaze.Interface.Repository
{
    /// <summary>
    /// Content-addressed storage for the media the app serves: cover images, avatars, and
    /// activity media. It is an abstraction over a bucket, not over a vendor — no member here
    /// names an Azure type, so callers depend on the operation rather than on the SDK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The container is a parameter of every operation</b>, not a property of the repository.
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
    public interface IStorageRepository
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
        /// The URL an object in a public container is read by, unsigned.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This sits beside <see cref="CreateReadUrlAsync"/> because the two are not
        /// interchangeable and choosing wrongly is silent. A SAS attached to a blob in a public
        /// container grants nothing the container had not already granted, and turns a link that
        /// was always public into one that dies — which is why feature 07 forbids exactly that.
        /// The container is the whole of the public/private answer, so a caller addressing a
        /// public one (<c>covers</c>, <c>avatars</c>) wants this, and a caller reaching a private
        /// one (<c>media</c>) wants the SAS.
        /// </para>
        /// <para>
        /// Being unsigned, the result does not expire and stays valid for as long as the
        /// container stays public, so it is safe to store. The cost is that it carries no
        /// authorization: handed a private container it returns a well-formed URL that fails on
        /// fetch. That is a caller error this cannot detect — the container is the caller's
        /// choice, and this abstraction is deliberately not told which containers are public.
        /// </para>
        /// </remarks>
        /// <param name="container">A public container: <c>Constant.StorageContainer.Covers</c> or
        /// <c>Avatars</c>.</param>
        /// <param name="path">Path within the container.</param>
        /// <returns>A URL readable without authorization.</returns>
        Uri CreatePublicUrl(string container, string path);

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
