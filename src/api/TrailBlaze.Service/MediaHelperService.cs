namespace TrailBlaze.Service;

using ImageMagick;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;

/// <inheritdoc/>
public sealed class MediaHelperService(int quality) : IMediaHelperService
{
    /// <summary>The quality to encode at: the configured value, or the default when none is
    /// configured, held inside the ceiling whatever it says.</summary>
    public uint AppliedQuality =>
        quality <= 0
            ? Constant.Upload.QualityDefault
            : Math.Min((uint)quality, Constant.Upload.QualityCeiling);

    /// <inheritdoc/>
    public async Task<Stream> GetThumbnailStreamAsync(Stream content)
    {
        content.Position = 0;

        using var image = new MagickImage(content);
        image.AutoOrient();
        image.Format = MagickFormat.WebP;
        image.Quality = AppliedQuality;

        // Deliberately not `using`: the caller reads this stream after this method returns, so a
        // scope-bound dispose would hand them a closed one.
        var derivative = new MemoryStream();
        await image.WriteAsync(derivative);
        derivative.Position = 0;

        return derivative;
    }
}
