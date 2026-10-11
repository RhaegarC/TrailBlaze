namespace TrailBlaze.Service.Test;

using ImageMagick;
using TrailBlaze.Model;
using TrailBlaze.Service.Test.TestSupport;

/// <summary>
/// The derivative producer, exercised against the real decoder with the real bytes.
/// </summary>
/// <remarks>
/// Offline and deterministic: the library is a managed assembly with bundled natives, so nothing here
/// needs a container, and every input is built by <see cref="ImageFixtures"/> rather than by the
/// library the assertions are about.
/// </remarks>
public sealed class MediaHelperServiceTests
{
    private static MediaHelperService Service(int quality = 0) => new(quality);

    private static Task<Stream> DerivativeOf(byte[] content, int quality = 0) =>
        Service(quality).GetThumbnailStreamAsync(new MemoryStream(content));

    /// <summary>The derivative's bytes, read the way the storage repository reads them: from the
    /// stream the producer handed back, after it returned.</summary>
    private static async Task<byte[]> DerivativeBytes(byte[] content, int quality = 0)
    {
        using Stream derivative = await DerivativeOf(content, quality);
        using var buffer = new MemoryStream();

        await derivative.CopyToAsync(buffer);

        return buffer.ToArray();
    }

    private static async Task<MagickImage> Decode(byte[] content, int quality = 0) =>
        new(await DerivativeBytes(content, quality));

    /// <summary>
    /// The stream is still open once the call has returned, because the caller uploads from it
    /// afterwards: a derivative disposed on the way out hands the caller a closed stream that every
    /// read rejects.
    /// </summary>
    [Fact]
    public async Task The_derivative_stream_is_readable_after_the_call_returns()
    {
        Stream derivative = await DerivativeOf(ImageFixtures.Gradient(400, 300));

        Assert.True(derivative.CanRead);

        byte[] bytes = new byte[derivative.Length];
        int read = await derivative.ReadAsync(bytes);

        Assert.Equal(bytes.Length, read);
        Assert.NotEmpty(bytes);
    }

    /// <summary>The derivative is re-encoded, not a copy of the original's bytes.</summary>
    [Fact]
    public async Task The_derivative_is_a_WebP_however_the_source_was_encoded()
    {
        byte[] source = ImageFixtures.Gradient(400, 300);

        using MagickImage image = await Decode(source);

        Assert.Equal(MagickFormat.WebP.ToString(), image.Format.ToString());
        Assert.NotEqual(source, await DerivativeBytes(source));
    }

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
    public async Task A_derivative_comes_back_smaller_than_the_photograph_it_came_from()
    {
        byte[] photo = ImageFixtures.Noisy(900, 700);

        byte[] derivative = await DerivativeBytes(photo);

        Assert.True(
            derivative.Length < photo.Length,
            $"The derivative ({derivative.Length} bytes) is not smaller than its source "
            + $"({photo.Length} bytes).");
    }

    /// <summary>A source with transparency keeps its shape: WebP carries an alpha channel, so the
    /// derivative is a still rather than a flattening encode.</summary>
    [Fact]
    public async Task A_transparent_source_keeps_its_size()
    {
        using MagickImage image = await Decode(ImageFixtures.Transparent(200, 150));

        Assert.Equal(200u, image.Width);
        Assert.Equal(150u, image.Height);
    }

    /// <summary>An animated source yields one frame, not a moving derivative.</summary>
    [Fact]
    public async Task An_animated_source_yields_a_single_frame()
    {
        using var frames = new MagickImageCollection(
            await DerivativeBytes(ImageFixtures.AnimatedGif(64, 64, frames: 3)));

        Assert.Single(frames);
    }

    /// <summary>
    /// Bytes that are not an image are reported to the caller rather than answered with an empty
    /// derivative: a media item keeps its original and survives the failure, and a caller that
    /// stored an empty stream in its place would be serving a broken image as a success.
    /// </summary>
    [Fact]
    public async Task Bytes_that_are_not_an_image_are_reported_rather_than_returned()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => DerivativeOf(ImageFixtures.NotAnImage));
    }

    /// <summary>The same answer for an empty file, which is what a client that sent nothing sends.</summary>
    [Fact]
    public async Task An_empty_file_is_reported_rather_than_returned()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => DerivativeOf([]));
    }

    /// <summary>
    /// The configured quality takes effect, which is only observable on an image with detail to
    /// discard: at quality 5 far more of the source survives as approximation than at quality 95.
    /// </summary>
    [Fact]
    public async Task The_configured_quality_takes_effect()
    {
        byte[] photo = ImageFixtures.Noisy(600, 400);

        byte[] lossy = await DerivativeBytes(photo, quality: 5);
        byte[] faithful = await DerivativeBytes(photo, quality: 95);

        Assert.True(
            lossy.Length < faithful.Length,
            $"Quality 5 produced {lossy.Length} bytes and quality 95 produced "
            + $"{faithful.Length}: the setting made no difference.");
    }

    /// <summary>A value inside the ceiling is honoured exactly, neither rounded nor padded.</summary>
    [Fact]
    public void A_value_inside_the_ceiling_is_honoured()
    {
        Assert.Equal(40u, Service(40).AppliedQuality);
    }

    /// <summary>A value above the ceiling is clamped to it rather than honoured.</summary>
    [Fact]
    public void A_value_above_the_ceiling_is_clamped_to_it()
    {
        Assert.Equal(Constant.Upload.QualityCeiling, Service(int.MaxValue).AppliedQuality);
    }

    /// <summary>A value that is not positive is the absence of a setting, not an instruction to
    /// encode at quality zero.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void A_value_that_is_not_positive_falls_back_to_the_default(int configured)
    {
        Assert.Equal(Constant.Upload.QualityDefault, Service(configured).AppliedQuality);
    }

    /// <summary>
    /// The default is the quality every unconfigured deployment encodes at, asserted rather than
    /// described: a silent change to it would change what every surface displays.
    /// </summary>
    [Fact]
    public void The_default_quality_is_the_one_the_feature_chose()
    {
        Assert.Equal(75u, Constant.Upload.QualityDefault);
        Assert.True(Constant.Upload.QualityDefault <= Constant.Upload.QualityCeiling);
    }
}
