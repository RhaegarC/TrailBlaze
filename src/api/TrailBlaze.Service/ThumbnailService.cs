namespace TrailBlaze.Service;

using ImageMagick;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.Media;

/// <inheritdoc/>
/// <remarks>The decoder's limits are process-wide because the library's are, and setting them in the
/// static constructor keeps every ImageMagick type inside this layer.</remarks>
public sealed class ThumbnailService(ThumbnailOptions options) : IThumbnailService
{
    static ThumbnailService()
    {
        // Width and height are what the decoders honour; an area limit alone does not stop a bomb.
        ResourceLimits.Width = (ulong)Constant.Thumbnail.DecoderMaxDimension;
        ResourceLimits.Height = (ulong)Constant.Thumbnail.DecoderMaxDimension;
        ResourceLimits.Memory = (ulong)Constant.Thumbnail.DecoderMemoryBytes;

        // Bounded rather than one: this limit refuses a decode, it does not truncate one, so a
        // single-frame list would reject every animation instead of reading its first frame.
        ResourceLimits.ListLength = Constant.Thumbnail.DecoderMaxFrames;

        // No spill: without this the limits above are advisory, because the pixel cache would page
        // the decode out to the container's volume instead of failing.
        ResourceLimits.Disk = 0;
    }

    /// <inheritdoc/>
    public ThumbnailResult? Create(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Named here: an empty array throws from an argument guard, which is not a MagickException.
        if (content.Length == 0)
        {
            return null;
        }

        try
        {
            // The header first, so the refusal is this method's decision and not a failed allocation.
            MagickImageInfo info = new(content);

            if (info.Width > (uint)Constant.Thumbnail.DecoderMaxDimension
                || info.Height > (uint)Constant.Thumbnail.DecoderMaxDimension)
            {
                return null;
            }

            using var image = new MagickImage(content);

            // Before the resize, so a photo taken on its side is measured the way it is shown.
            image.AutoOrient();

            Resize(image);

            image.Quality = (uint)options.AppliedQuality;

            // Orientation is applied above; the rest of the metadata is dropped, so the original
            // stays the only copy carrying it.
            image.Strip();
            image.Format = MagickFormat.Jpeg;

            return new ThumbnailResult(image.ToByteArray(), Constant.Thumbnail.ContentType);
        }
        catch (MagickException)
        {
            // Undecodable, or refused by the limits above: either way the caller stores the original.
            return null;
        }
    }

    /// <summary>Brings the long edge down to the configured maximum, and never up: an image already
    /// inside the maximum is stored as it is.</summary>
    private void Resize(MagickImage image)
    {
        uint max = (uint)options.AppliedMaxDimension;

        if (image.Width <= max && image.Height <= max)
        {
            return;
        }

        // The zero means "proportional": naming both would distort, naming the short edge would
        // leave the long one over.
        image.Resize(image.Width >= image.Height
            ? new MagickGeometry(max, 0)
            : new MagickGeometry(0, max));
    }
}
