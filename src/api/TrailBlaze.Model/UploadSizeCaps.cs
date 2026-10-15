namespace TrailBlaze.Model;

/// <summary>The size caps an upload is held to, and what configuration was allowed to make of them.</summary>
/// <remarks>
/// Configuration may raise a cap to its ceiling and no further: the ceiling is what still refuses a
/// runaway upload, and it is also what the routes carry as their request-size limit, so a cap past it
/// would answer a file the setting admitted with a bare 413 instead of the refusal naming the cap.
/// This is the shape <see cref="SignedUrlLifetime"/> gives the SAS window, for the same reason.
/// </remarks>
/// <param name="ConfiguredImageBytes">What configuration asked for, which is not necessarily what is applied.</param>
/// <param name="ConfiguredVideoBytes">What configuration asked for, which is not necessarily what is applied.</param>
public sealed record UploadSizeCaps(long ConfiguredImageBytes, long ConfiguredVideoBytes)
{
    /// <summary>The image cap actually applied, which is <see cref="ConfiguredImageBytes"/> inside the ceiling.</summary>
    public long AppliedImageBytes =>
        Apply(
            ConfiguredImageBytes,
            Constant.Upload.DefaultImageSizeCapBytes,
            Constant.Upload.MaxImageSizeCapBytes);

    /// <summary>The video cap actually applied, which is <see cref="ConfiguredVideoBytes"/> inside the ceiling.</summary>
    public long AppliedVideoBytes =>
        Apply(
            ConfiguredVideoBytes,
            Constant.Upload.DefaultVideoSizeCapBytes,
            Constant.Upload.MaxVideoSizeCapBytes);

    /// <summary>A value that is not positive is the absence of a setting, so it takes the default.</summary>
    private static long Apply(long configured, long fallback, long ceiling) =>
        configured <= 0 ? fallback
        : configured > ceiling ? ceiling
        : configured;
}
