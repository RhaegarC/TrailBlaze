namespace TrailBlaze.Interface.Service;

using TrailBlaze.Model;

/// <summary>
/// Decides whether an uploaded file is one this app will store, against the shared allowlists
/// and size caps.
/// </summary>
/// <remarks>
/// <para>
/// This decides only what may be <em>stored</em>. It says nothing about whether the caller is
/// allowed to store it, how many they may store, or where it goes; the first two are route
/// rules and belong to the feature that owns each route, and the third is the container the
/// caller picked.
/// </para>
/// <para>
/// Every member returns <c>null</c> when the upload is acceptable and the message to show the
/// user when it is not — a rejected upload is an ordinary answer, not a fault, and returning
/// the text rather than a code keeps the wording in the layer that owns the rule.
/// </para>
/// </remarks>
public interface IUploadValidationService
{
    /// <summary>Validates an image against the shared image allowlist and 10 MB cap.</summary>
    /// <param name="contentType">The declared content type, as sent by the client.</param>
    /// <param name="sizeBytes">The number of bytes the upload carries.</param>
    /// <returns>Null when acceptable, otherwise the reason to return to the client.</returns>
    string? ValidateImage(string? contentType, long sizeBytes);

    /// <summary>Validates a video against the video allowlist and 200 MB cap. Unused until
    /// feature 06, which is the point of the allowlist living in one place.</summary>
    /// <param name="contentType">The declared content type, as sent by the client.</param>
    /// <param name="sizeBytes">The number of bytes the upload carries.</param>
    /// <returns>Null when acceptable, otherwise the reason to return to the client.</returns>
    string? ValidateVideo(string? contentType, long sizeBytes);

    /// <summary>
    /// The kind a content type belongs to, or <c>null</c> for a type neither allowlist admits.
    /// </summary>
    /// <remarks>
    /// Media is the one route that accepts two kinds, so it is the one route that has to ask which
    /// a file is. Answered here rather than at the route, because the answer is the same membership
    /// test <see cref="ValidateImage"/> and <see cref="ValidateVideo"/> perform, and a second copy
    /// of it is a second idea of what an image is.
    /// </remarks>
    /// <param name="contentType">The declared content type, as sent by the client.</param>
    /// <returns>One of <see cref="Constant.MediaKind"/>, or null when it is neither.</returns>
    string? MediaKindOf(string? contentType);

    /// <summary>
    /// The file extension a stored object is named with, or an empty string for a type this
    /// app does not recognize.
    /// </summary>
    /// <param name="contentType">A validated content type.</param>
    string FileExtensionFor(string contentType);
}
