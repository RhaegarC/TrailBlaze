namespace TrailBlaze.Interface.Service;

using TrailBlaze.Model.Media;

/// <summary>
/// Produces the smaller copy an uploaded image is served as.
/// </summary>
/// <remarks>
/// <para>
/// <b>A failure is answered with <c>null</c> and never with an exception.</b> The uploader supplies
/// the file, so "this is not an image" and "this image is too large to decode safely" are ordinary
/// answers rather than faults — and the caller stores the original either way, because the
/// derivative is an optimization of the item rather than the item.
/// </para>
/// <para>
/// <b>The bytes handed in are a decoded attacker-controlled document.</b> The implementation is
/// where the resource limits live, and they are set once for the process rather than per call, so
/// the decision not to decode is made before the allocation happens.
/// </para>
/// </remarks>
public interface IThumbnailService
{
    /// <summary>A derivative of <paramref name="content"/>, or null when none can be produced.</summary>
    /// <param name="content">The uploaded bytes, exactly as they arrived.</param>
    /// <returns>The derivative, or null when the bytes are not a decodable image of a size this
    /// application will decode.</returns>
    ThumbnailResult? Create(byte[] content);
}
