namespace TrailBlaze.Service.Test;

using TrailBlaze.Model;

/// <summary>What configuration is allowed to do to the derivative's shape, decided from two numbers
/// alone.</summary>
public sealed class ThumbnailOptionsTests
{
    /// <summary>A value inside the ceiling is honoured exactly, neither rounded nor padded.</summary>
    [Fact]
    public void A_value_inside_the_ceiling_is_honoured()
    {
        var options = new ThumbnailOptions(40, 2400);

        Assert.Equal(40, options.AppliedQuality);
        Assert.Equal(2400, options.AppliedMaxDimension);
    }

    /// <summary>A value above the ceiling is clamped to it rather than honoured.</summary>
    [Fact]
    public void A_value_above_the_ceiling_is_clamped_to_it()
    {
        var options = new ThumbnailOptions(int.MaxValue, int.MaxValue);

        Assert.Equal(Constant.Thumbnail.QualityCeiling, options.AppliedQuality);
        Assert.Equal(Constant.Thumbnail.MaxDimensionCeiling, options.AppliedMaxDimension);
    }

    /// <summary>A value that is not positive is the absence of a setting, not an instruction to
    /// encode at quality zero or to store nothing.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void A_value_that_is_not_positive_falls_back_to_the_default(int configured)
    {
        var options = new ThumbnailOptions(configured, configured);

        Assert.Equal(Constant.Thumbnail.QualityDefault, options.AppliedQuality);
        Assert.Equal(Constant.Thumbnail.MaxDimensionDefault, options.AppliedMaxDimension);
    }

    /// <summary>The defaults are themselves inside their ceilings, so the fallback is not a way past
    /// the bound.</summary>
    [Fact]
    public void The_defaults_are_inside_their_ceilings()
    {
        Assert.InRange(Constant.Thumbnail.QualityDefault, 0, Constant.Thumbnail.QualityCeiling);
        Assert.InRange(
            Constant.Thumbnail.MaxDimensionDefault, 0, Constant.Thumbnail.MaxDimensionCeiling);
    }

    /// <summary>One setting being clamped says nothing about the other, which is what makes these
    /// two settings and not one.</summary>
    [Fact]
    public void The_two_settings_are_clamped_apart()
    {
        var options = new ThumbnailOptions(0, int.MaxValue);

        Assert.Equal(Constant.Thumbnail.QualityDefault, options.AppliedQuality);
        Assert.Equal(Constant.Thumbnail.MaxDimensionCeiling, options.AppliedMaxDimension);
    }

    /// <summary>
    /// The quality default is the very lossy one the feature was designed around, asserted rather
    /// than described: a silent change to it would change what every surface displays.
    /// </summary>
    [Fact]
    public void The_default_quality_is_the_one_the_feature_chose()
    {
        var options = new ThumbnailOptions(0, 0);

        Assert.Equal(5, options.AppliedQuality);
    }
}
