namespace TrailBlaze.Interface.Service;

/// <summary>
/// The smaller copy of an uploaded image that a browser is served in place of the original
/// (Decision #31).
/// </summary>
public interface IMediaHelperService
{
    /// <summary>
    /// Re-encodes <paramref name="content"/> as the derivative.
    /// </summary>
    /// <remarks>
    /// The returned stream belongs to the caller, who reads it after this call returns. Bytes that
    /// are not a decodable image throw rather than yield an empty derivative: a media item keeps its
    /// original and so survives that, while a cover has nothing to fall back to and the caller
    /// decides what a failure means.
    /// </remarks>
    /// <param name="content">The uploaded bytes, left open and rewound to its start.</param>
    /// <returns>The derivative, positioned at its start.</returns>
    Task<Stream> GetThumbnailStreamAsync(Stream content);
}
