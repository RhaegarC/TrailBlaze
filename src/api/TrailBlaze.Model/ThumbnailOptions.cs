namespace TrailBlaze.Model;

/// <summary>
/// The derivative's quality and maximum dimension, as configuration asked for them.
/// </summary>
/// <remarks>
/// Clamped the way <see cref="UploadSizeCaps"/> clamps its caps and <see cref="SignedUrlLifetime"/>
/// clamps its window: absent, unreadable or non-positive is the absence of a setting rather than an
/// instruction, and a value above the ceiling is held at it rather than honoured.
/// </remarks>
/// <param name="ConfiguredQuality">The quality the derivative is encoded at, or zero when nothing
/// configured one.</param>
/// <param name="ConfiguredMaxDimension">The longest edge the derivative may have, or zero when
/// nothing configured one.</param>
public sealed record ThumbnailOptions(int ConfiguredQuality, int ConfiguredMaxDimension)
{
    /// <summary>The quality to encode at.</summary>
    public int AppliedQuality =>
        Apply(ConfiguredQuality, Constant.Thumbnail.QualityDefault, Constant.Thumbnail.QualityCeiling);

    /// <summary>The longest edge the derivative may have.</summary>
    public int AppliedMaxDimension => Apply(
        ConfiguredMaxDimension,
        Constant.Thumbnail.MaxDimensionDefault,
        Constant.Thumbnail.MaxDimensionCeiling);

    /// <summary>A value that is not positive is the absence of a setting, so it takes the default.</summary>
    private static int Apply(int configured, int fallback, int ceiling) =>
        configured <= 0 ? fallback
        : configured > ceiling ? ceiling
        : configured;
}
