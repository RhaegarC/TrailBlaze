namespace TrailBlaze.Service;

using TrailBlaze.Interface.Service;
using TrailBlaze.Model;

/// <summary>
/// The shared upload rules: the allowlists in <see cref="Constant.Upload"/> and the size caps
/// configuration set.
/// </summary>
/// <remarks>
/// Shared by every upload route — an avatar, a cover and activity media — so the three cannot answer
/// "is this an image?" differently or hold it to a different size. The cap comes from
/// <see cref="UploadSizeCaps"/> rather than from a constant, and the refusal is worded from the same
/// number, so a raised cap changes both together; that is also why this is injected rather than
/// static.
/// </remarks>
public sealed class UploadValidationService(UploadSizeCaps caps) : IUploadValidationService
{
    /// <inheritdoc/>
    public string? ValidateImage(string? contentType, long sizeBytes)
    {
        long capBytes = caps.AppliedImageBytes;

        return Validate(
            contentType,
            sizeBytes,
            Constant.Upload.ImageContentTypes,
            capBytes,
            Constant.Message.ImageTypeNotAllowed,
            Constant.Message.ImageTooLarge(capBytes));
    }

    /// <inheritdoc/>
    public string? ValidateVideo(string? contentType, long sizeBytes)
    {
        long capBytes = caps.AppliedVideoBytes;

        return Validate(
            contentType,
            sizeBytes,
            Constant.Upload.VideoContentTypes,
            capBytes,
            Constant.Message.VideoTypeNotAllowed,
            Constant.Message.VideoTooLarge(capBytes));
    }

    /// <inheritdoc/>
    public string? MediaKindOf(string? contentType) =>
        Constant.Upload.ImageContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)
            ? Constant.MediaKind.Image
            : Constant.Upload.VideoContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)
                ? Constant.MediaKind.Video
                : null;

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
