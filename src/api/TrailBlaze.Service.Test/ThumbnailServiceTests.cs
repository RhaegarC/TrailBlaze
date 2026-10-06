namespace TrailBlaze.Service.Test;

using ImageMagick;
using TrailBlaze.Model;
using TrailBlaze.Model.Media;
using TrailBlaze.Service.Test.TestSupport;

/// <summary>
/// The derivative producer, exercised against the real decoder with the real bytes.
/// </summary>
/// <remarks>
/// Offline and deterministic: the library is a managed assembly with bundled natives, so nothing here
/// needs a container, and every input is built by <see cref="ImageFixtures"/> rather than by the
/// library the assertions are about.
/// </remarks>
public sealed class ThumbnailServiceTests
{
    private static ThumbnailService Service(int quality = 0, int maxDimension = 0) =>
        new(new ThumbnailOptions(quality, maxDimension));

    private static ThumbnailResult DerivativeOf(byte[] content, int quality = 0, int maxDimension = 0) =>
        Service(quality, maxDimension).Create(content)
        ?? throw new Xunit.Sdk.XunitException("No derivative was produced.");

    private static MagickImage Decode(ThumbnailResult result) => new(result.Content);

    /// <summary>
    /// The claim the whole feature exists for: what the browser receives is smaller than what was
    /// uploaded.
    /// </summary>
    /// <remarks>
    /// The source is noise on purpose. A smooth image compresses so well as a PNG that a lossy
    /// re-encode of it can be larger, which would make this assertion about the format rather than
    /// about the derivative; a photograph is what the feature is for, and noise is what a photograph
    /// is to a compressor.
    /// </remarks>
    [Fact]
    public void A_derivative_comes_back_smaller_than_the_photograph_it_came_from()
    {
        byte[] photo = ImageFixtures.Noisy(900, 700);

        ThumbnailResult result = DerivativeOf(photo);

        Assert.True(
            result.Content.Length < photo.Length,
            $"The derivative ({result.Content.Length} bytes) is not smaller than its source "
            + $"({photo.Length} bytes).");
    }

    /// <summary>The derivative is re-encoded, not a copy of the original's bytes.</summary>
    [Fact]
    public void The_derivative_is_a_JPEG_however_the_original_was_encoded()
    {
        ThumbnailResult result = DerivativeOf(ImageFixtures.Gradient(400, 300));

        Assert.Equal(Constant.Thumbnail.ContentType, result.ContentType);
        Assert.Equal("Jpeg", Decode(result).Format.ToString());
    }

    /// <summary>
    /// The long edge is brought down to the configured maximum, whichever edge that is.
    /// </summary>
    [Theory]
    [InlineData(3000, 2000, 1600, 1067)]
    [InlineData(2000, 3000, 1067, 1600)]
    public void The_long_edge_is_brought_down_to_the_configured_maximum(
        int width, int height, int expectedWidth, int expectedHeight)
    {
        ThumbnailResult result = DerivativeOf(ImageFixtures.Gradient(width, height), maxDimension: 1600);

        using MagickImage image = Decode(result);

        Assert.Equal((uint)expectedWidth, image.Width);
        Assert.Equal((uint)expectedHeight, image.Height);
    }

    /// <summary>
    /// An image already inside the maximum keeps its size: the derivative is a bound, not a target,
    /// and enlarging a small picture to meet it would spend bytes to lose sharpness.
    /// </summary>
    [Fact]
    public void An_image_inside_the_maximum_is_not_enlarged()
    {
        ThumbnailResult result = DerivativeOf(ImageFixtures.Gradient(100, 80), maxDimension: 1600);

        using MagickImage image = Decode(result);

        Assert.Equal(100u, image.Width);
        Assert.Equal(80u, image.Height);
    }

    /// <summary>A configured maximum is applied rather than the default one.</summary>
    [Fact]
    public void The_configured_maximum_is_the_bound_that_applies()
    {
        ThumbnailResult result = DerivativeOf(ImageFixtures.Gradient(1200, 900), maxDimension: 200);

        using MagickImage image = Decode(result);

        Assert.Equal(200u, image.Width);
        Assert.Equal(150u, image.Height);
    }

    /// <summary>
    /// The configured quality takes effect, which is only observable on an image with detail to
    /// discard: at quality 5 far more of the source survives as approximation than at quality 95.
    /// </summary>
    [Fact]
    public void The_configured_quality_takes_effect()
    {
        byte[] photo = ImageFixtures.Noisy(600, 400);

        ThumbnailResult lossy = DerivativeOf(photo, quality: 5);
        ThumbnailResult faithful = DerivativeOf(photo, quality: 95);

        Assert.True(
            lossy.Content.Length < faithful.Content.Length,
            $"Quality 5 produced {lossy.Content.Length} bytes and quality 95 produced "
            + $"{faithful.Content.Length}: the setting made no difference.");
    }

    /// <summary>A source with transparency becomes a JPEG, which has no alpha channel at all —
    /// so the derivative is a still rather than a broken encode.</summary>
    [Fact]
    public void A_transparent_source_becomes_a_still_JPEG()
    {
        ThumbnailResult result = DerivativeOf(ImageFixtures.Transparent(200, 150));

        using MagickImage image = Decode(result);

        Assert.Equal("Jpeg", image.Format.ToString());
        Assert.Equal(200u, image.Width);
    }

    /// <summary>An animated source yields one frame, not a moving derivative.</summary>
    [Fact]
    public void An_animated_source_yields_a_single_frame()
    {
        ThumbnailResult result = DerivativeOf(ImageFixtures.AnimatedGif(64, 64, frames: 3));

        using var frames = new MagickImageCollection(result.Content);

        Assert.Single(frames.Select(frame => frame.Width));
    }

    /// <summary>
    /// Bytes that are not an image are answered with nothing rather than with an exception: the
    /// uploader supplied the file, and the caller stores the original either way.
    /// </summary>
    [Fact]
    public void Bytes_that_are_not_an_image_yield_no_derivative()
    {
        Assert.Null(Service().Create(ImageFixtures.NotAnImage));
    }

    /// <summary>The same answer for an empty file, which is what a client that sent nothing sends.</summary>
    [Fact]
    public void An_empty_file_yields_no_derivative()
    {
        Assert.Null(Service().Create([]));
    }

    /// <summary>
    /// A file whose header claims a size past the decoder's limit is refused undecoded, and the
    /// refusal is this method's own decision rather than an allocation the library attempted.
    /// </summary>
    /// <remarks>
    /// The fixture is a one-pixel-tall strip rather than a real bomb, because a genuine bomb cannot
    /// be written to a file and stored in a repository — and it does not need to be: the boundary is
    /// what is being asserted, so the image on either side of it is valid and tiny.
    /// </remarks>
    [Fact]
    public void An_image_past_the_decoders_dimension_limit_yields_no_derivative()
    {
        byte[] tooWide = ImageFixtures.Gradient(Constant.Thumbnail.DecoderMaxDimension + 1, 1);
        byte[] atTheLimit = ImageFixtures.Gradient(Constant.Thumbnail.DecoderMaxDimension, 1);

        Assert.Null(Service().Create(tooWide));
        Assert.NotNull(Service().Create(atTheLimit));
    }

    /// <summary>
    /// The limit is a bound on the decoded pixels and not on the bytes that arrived: a small file
    /// can describe an enormous image, which is the shape of the attack the limit is for.
    /// </summary>
    [Fact]
    public void A_small_file_describing_an_enormous_image_is_judged_on_what_it_describes()
    {
        byte[] small = ImageFixtures.Gradient(Constant.Thumbnail.DecoderMaxDimension + 1, 1);

        Assert.True(
            small.Length < 4096,
            $"The fixture is {small.Length} bytes, which is not small enough to make the point.");
        Assert.Null(Service().Create(small));
    }
}
