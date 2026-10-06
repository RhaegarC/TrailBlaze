namespace TrailBlaze.Model.Media;

/// <summary>
/// The smaller copy derived from an uploaded image, and what it is encoded as.
/// </summary>
/// <remarks>
/// Carries its own content type rather than letting the caller assume one: the derivative is stored
/// through the same repository call the original is, and a type named at the call site is a second
/// place the encoding decision lives.
/// </remarks>
/// <param name="Content">The encoded derivative.</param>
/// <param name="ContentType">The content type it is stored and served under.</param>
public sealed record ThumbnailResult(byte[] Content, string ContentType);
