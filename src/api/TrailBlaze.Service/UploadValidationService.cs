using TrailBlaze.Model;

namespace TrailBlaze.Service
{
    /// <summary>
    /// Decides whether an uploaded file is one this app will store, against the allowlist and the
    /// size caps in <see cref="Constant.Upload"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by every upload route — an avatar here, a cover in feature 08, activity media in
    /// feature 06 — so the three cannot answer "is this an image?" differently. The rules are not
    /// per-route: an image is the same set of content types whether it is a face or a landscape,
    /// and the size cap is the same 10 MB.
    /// </para>
    /// <para>
    /// This decides only what may be <em>stored</em>. It says nothing about whether the caller is
    /// allowed to store it, how many they may store, or where it goes; the first two are route
    /// rules and belong to the feature that owns each route, and the third is the container the
    /// caller picked.
    /// </para>
    /// <para>
    /// Every method returns <c>null</c> when the upload is acceptable and the message to show the
    /// user when it is not — a rejected upload is an ordinary answer, not a fault, and returning
    /// the text rather than a code keeps the wording in the layer that owns the rule.
    /// </para>
    /// </remarks>
    public sealed class UploadValidationService
    {
        /// <summary>Validates an image against the shared image allowlist and 10 MB cap.</summary>
        /// <param name="contentType">The declared content type, as sent by the client.</param>
        /// <param name="sizeBytes">The number of bytes the upload carries.</param>
        /// <returns>Null when acceptable, otherwise the reason to return to the client.</returns>
        public string? ValidateImage(string? contentType, long sizeBytes) =>
            Validate(
                contentType,
                sizeBytes,
                Constant.Upload.ImageContentTypes,
                Constant.Upload.ImageSizeCapBytes,
                Constant.Message.ImageTypeNotAllowed,
                Constant.Message.ImageTooLarge);

        /// <summary>Validates a video against the video allowlist and 200 MB cap. Unused until
        /// feature 06, which is the point of the allowlist living in one place.</summary>
        /// <param name="contentType">The declared content type, as sent by the client.</param>
        /// <param name="sizeBytes">The number of bytes the upload carries.</param>
        /// <returns>Null when acceptable, otherwise the reason to return to the client.</returns>
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
        public static string FileExtensionFor(string contentType) =>
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
}
