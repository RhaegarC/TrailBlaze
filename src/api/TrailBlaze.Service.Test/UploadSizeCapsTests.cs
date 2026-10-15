namespace TrailBlaze.Service.Test;

using TrailBlaze.Model;

/// <summary>What configuration is allowed to do to the upload caps, decided from two numbers alone.</summary>
public sealed class UploadSizeCapsTests
{
    /// <summary>A cap inside the ceiling is honoured exactly, neither rounded nor padded.</summary>
    [Fact]
    public void A_cap_inside_the_ceiling_is_honoured()
    {
        var caps = new UploadSizeCaps(50L * 1024 * 1024, 400L * 1024 * 1024);

        Assert.Equal(50L * 1024 * 1024, caps.AppliedImageBytes);
        Assert.Equal(400L * 1024 * 1024, caps.AppliedVideoBytes);
    }

    /// <summary>A cap above the ceiling is clamped to it rather than honoured.</summary>
    [Fact]
    public void A_cap_above_the_ceiling_is_clamped_to_it()
    {
        var caps = new UploadSizeCaps(long.MaxValue, long.MaxValue);

        Assert.Equal(Constant.Upload.MaxImageSizeCapBytes, caps.AppliedImageBytes);
        Assert.Equal(Constant.Upload.MaxVideoSizeCapBytes, caps.AppliedVideoBytes);
    }

    /// <summary>A cap that is not positive is the absence of a setting, not an instruction to refuse everything.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void A_cap_that_is_not_positive_falls_back_to_the_default(long bytes)
    {
        var caps = new UploadSizeCaps(bytes, bytes);

        Assert.Equal(Constant.Upload.DefaultImageSizeCapBytes, caps.AppliedImageBytes);
        Assert.Equal(Constant.Upload.DefaultVideoSizeCapBytes, caps.AppliedVideoBytes);
    }

    /// <summary>The default is itself inside the ceiling, so the fallback is not a way past the bound.</summary>
    [Fact]
    public void The_default_is_inside_the_ceiling()
    {
        Assert.InRange(
            Constant.Upload.DefaultImageSizeCapBytes, 0, Constant.Upload.MaxImageSizeCapBytes);
        Assert.InRange(
            Constant.Upload.DefaultVideoSizeCapBytes, 0, Constant.Upload.MaxVideoSizeCapBytes);
    }

    /// <summary>One kind being clamped says nothing about the other, which is what makes these two settings and not one.</summary>
    [Fact]
    public void The_two_kinds_are_clamped_apart()
    {
        var caps = new UploadSizeCaps(long.MaxValue, 0);

        Assert.Equal(Constant.Upload.MaxImageSizeCapBytes, caps.AppliedImageBytes);
        Assert.Equal(Constant.Upload.DefaultVideoSizeCapBytes, caps.AppliedVideoBytes);
    }
}
