namespace TrailBlaze.Service;

using TrailBlaze.Interface.Service;
using TrailBlaze.Model;

/// <summary>
/// The shared upload rules, read from the allowlists and size caps in
/// <see cref="Constant.Upload"/>.
/// </summary>
/// <remarks>
/// <para>
/// Shared by every upload route — an avatar here, a cover in feature 08, activity media in
/// feature 06 — so the three cannot answer "is this an image?" differently. The rules are not
/// per-route: an image is the same set of content types whether it is a face or a landscape,
/// and the size cap is the same 10 MB.
/// </para>
/// <para>
/// Stateless, so it is registered as a singleton. It is injected rather than kept static so
/// the routes depend on <see cref="IUploadValidationService"/>: if a cap ever needs to come
/// from configuration rather than <see cref="Constant"/>, that changes here and no caller
/// changes at all.
/// </para>
/// </remarks>
public sealed class UploadValidationService : IUploadValidationService
{
    /// <inheritdoc/>
    public string? ValidateImage(string? contentType, long sizeBytes) =>
        Validate(
            contentType,
            sizeBytes,
            Constant.Upload.ImageContentTypes,
            Constant.Upload.ImageSizeCapBytes,
            Constant.Message.ImageTypeNotAllowed,
            Constant.Message.ImageTooLarge);

    /// <inheritdoc/>
    public string? ValidateVideo(string? contentType, long sizeBytes) =>
        Validate(
            contentType,
            sizeBytes,
            Constant.Upload.VideoContentTypes,
            Constant.Upload.VideoSizeCapBytes,
            Constant.Message.VideoTypeNotAllowed,
            Constant.Message.VideoTooLarge);

    /// <summary>
    /// The file extension a stored object is named with, or an empty string for a type this
    /// app does not recognize.
    /// </summary>
    /// <remarks>
    /// Cosmetic, and deliberately not throwing on an unknown type: what actually serves the
    /// object back correctly is the content type recorded on the blob, not the suffix on its
    /// path. Callers validate first, so an unrecognized type here is a mistake rather than a
    /// threat, and failing the request over a suffix would be the tail wagging the dog.
    /// </remarks>
    /// <param name="contentType">A validated content type.</param>
    public string FileExtensionFor(string contentType) =>
        contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "video/mp4" => ".mp4",
            "video/quicktime" => ".mov",
            _ => string.Empty,
        };

    private static string? Validate(
        string? contentType,
        long sizeBytes,
        string[] allowedContentTypes,
        long sizeCapBytes,
        string typeMessage,
        string sizeMessage)
    {
        // Case-insensitive: a content type is case-insensitive per RFC 9110, so a client
        // sending IMAGE/JPEG is sending a valid request and rejecting it would be this app's
        // bug rather than the client's.
        if (string.IsNullOrWhiteSpace(contentType)
            || !allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return typeMessage;
        }

        // The cap is inclusive — a file exactly at it is accepted, and only one byte over is
        // rejected. Said out loud because an exclusive comparison is the easy slip, and it
        // silently shaves a byte off the documented limit.
        //
        // A negative length is rejected too: it can only mean the size was never determined,
        // and a size that is unknown cannot be shown to be within the cap.
        if (sizeBytes < 0 || sizeBytes > sizeCapBytes)
        {
            return sizeMessage;
        }

        return null;
    }
}
