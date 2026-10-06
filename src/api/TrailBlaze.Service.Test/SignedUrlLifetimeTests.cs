namespace TrailBlaze.Service.Test;

using TrailBlaze.Model;

/// <summary>
/// The window a signed URL is given, decided from configuration alone.
/// </summary>
/// <remarks>
/// This is the whole of the control, which is why it is asserted here rather than only through the
/// routes that use it: a SAS is a bearer token, so the string is a credential for as long as it
/// lives, and the only thing standing between a leaked URL and the bytes is that it stops being
/// accepted. The rule is therefore a bounded window, and configuration may shorten it but must not
/// be able to lengthen it past the cap.
/// </remarks>
public sealed class SignedUrlLifetimeTests
{
    /// <summary>A window inside the cap is honoured exactly, neither rounded up nor padded.</summary>
    [Fact]
    public void A_window_inside_the_cap_is_honoured()
    {
        var lifetime = new SignedUrlLifetime(TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(10), lifetime.Applied);
    }

    /// <summary>
    /// A window above the cap is clamped to it rather than honoured, which is the one direction
    /// configuration is not allowed to move.
    /// </summary>
    /// <remarks>
    /// The value is absurd on purpose: "longer than the cap" and "effectively forever" are the same
    /// mistake, and a test that used a value just over the cap would still pass if the clamp
    /// multiplied or added rather than capped.
    /// </remarks>
    [Fact]
    public void A_window_above_the_cap_is_clamped_to_it()
    {
        var lifetime = new SignedUrlLifetime(TimeSpan.FromDays(365));

        Assert.Equal(SignedUrlLifetime.Maximum, lifetime.Applied);
    }

    /// <summary>
    /// A window that is not positive is the absence of a setting, not an instruction to mint a link
    /// that is already dead.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1440)]
    public void A_window_that_is_not_positive_falls_back_to_the_default(int minutes)
    {
        var lifetime = new SignedUrlLifetime(TimeSpan.FromMinutes(minutes));

        Assert.Equal(SignedUrlLifetime.Default, lifetime.Applied);
    }

    /// <summary>
    /// The default is itself inside the cap, so the fallback cannot be the way past the bound the
    /// clamp exists to enforce.
    /// </summary>
    [Fact]
    public void The_default_is_inside_the_cap()
    {
        Assert.InRange(SignedUrlLifetime.Default, TimeSpan.Zero, SignedUrlLifetime.Maximum);
    }

    /// <summary>The instant an applied window lands on, measured from the moment it was asked for.</summary>
    /// <remarks>
    /// The lower bound is a whole boundary minute short of the window, not a second: the minting
    /// instant is rounded down before the window is added, so a mint late in its boundary minute is
    /// up to that much shorter. See <see cref="A_boundary_mint_shortens_the_window_by_less_than_one_boundary"/>.
    /// </remarks>
    [Fact]
    public void The_expiry_is_the_applied_window_after_the_instant_it_was_given()
    {
        var lifetime = new SignedUrlLifetime(TimeSpan.FromMinutes(20));
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Assert.InRange(
            lifetime.ExpiryFrom(now) - now,
            TimeSpan.FromMinutes(20) - TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(20));
    }

    /// <summary>
    /// The expiry carries no sub-second part, because a SAS cannot.
    /// </summary>
    /// <remarks>
    /// The token's own <c>se</c> is written to the second, so an instant with a fractional part is
    /// truncated on the way in. A caller told <c>12:00:00.7</c> would therefore be reporting a moment
    /// the token does not hold, and would treat a link as live for most of a second after the server
    /// had stopped accepting it. Rounding here is what keeps the instant reported and the instant
    /// signed the same one.
    /// </remarks>
    [Fact]
    public void The_expiry_has_no_sub_second_part()
    {
        var lifetime = new SignedUrlLifetime(TimeSpan.FromMinutes(20));

        DateTimeOffset expiry = lifetime.ExpiryFrom(
            new DateTimeOffset(2026, 3, 14, 9, 30, 15, 731, TimeSpan.Zero));

        Assert.Equal(0, expiry.UtcTicks % TimeSpan.TicksPerSecond);
        Assert.Equal(
            new DateTimeOffset(2026, 3, 14, 9, 50, 0, TimeSpan.Zero),
            expiry);
    }

    /// <summary>
    /// Two mints inside one boundary minute produce one instant, which is what makes the URL string
    /// stable enough for a browser to reuse.
    /// </summary>
    /// <remarks>
    /// The URL is what a browser caches under, and a SAS carries its expiry — so two mints of an
    /// unchanged signature differ in exactly that one field, and a URL that moves is a URL a cache
    /// misses. Rounding the minting instant down collapses every mint in a minute onto one string.
    /// </remarks>
    [Fact]
    public void Two_mints_inside_one_boundary_minute_land_on_the_same_instant()
    {
        var lifetime = new SignedUrlLifetime(TimeSpan.FromMinutes(20));

        DateTimeOffset early = lifetime.ExpiryFrom(
            new DateTimeOffset(2026, 3, 14, 9, 30, 15, TimeSpan.Zero));
        DateTimeOffset late = lifetime.ExpiryFrom(
            new DateTimeOffset(2026, 3, 14, 9, 30, 59, 999, TimeSpan.Zero));

        Assert.Equal(early, late);
        Assert.Equal(new DateTimeOffset(2026, 3, 14, 9, 50, 0, TimeSpan.Zero), early);
    }

    /// <summary>
    /// A minute apart is a different instant, so the rounding collapses a minute rather than the whole
    /// window.
    /// </summary>
    /// <remarks>
    /// The pair straddles the boundary by one millisecond on purpose: rounding that was too coarse —
    /// to the hour, say — would satisfy the test above and fail here.
    /// </remarks>
    [Fact]
    public void Mints_on_either_side_of_a_boundary_do_not_land_on_the_same_instant()
    {
        var lifetime = new SignedUrlLifetime(TimeSpan.FromMinutes(20));

        DateTimeOffset justBefore = lifetime.ExpiryFrom(
            new DateTimeOffset(2026, 3, 14, 9, 30, 59, 999, TimeSpan.Zero));
        DateTimeOffset justAfter = lifetime.ExpiryFrom(
            new DateTimeOffset(2026, 3, 14, 9, 31, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 3, 14, 9, 50, 0, TimeSpan.Zero), justBefore);
        Assert.Equal(new DateTimeOffset(2026, 3, 14, 9, 51, 0, TimeSpan.Zero), justAfter);
    }

    /// <summary>
    /// The rounding shortens a URL's life and never lengthens it, and the cap still holds.
    /// </summary>
    /// <remarks>
    /// This is the cost of stability, stated rather than hidden: a URL minted late in its boundary
    /// minute lives up to one boundary less than the window asks for. The cap is the one direction
    /// that must not move, so a mint on a boundary is asserted against it too — rounding down can
    /// shorten a window but can never be the way past the bound.
    /// </remarks>
    [Fact]
    public void A_boundary_mint_shortens_the_window_by_less_than_one_boundary()
    {
        var lifetime = new SignedUrlLifetime(TimeSpan.FromMinutes(20));
        DateTimeOffset now = new DateTimeOffset(2026, 3, 14, 9, 30, 15, 731, TimeSpan.Zero);

        DateTimeOffset expiry = lifetime.ExpiryFrom(now);

        Assert.InRange(
            expiry - now,
            TimeSpan.FromMinutes(20) - TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(20));
    }

    /// <summary>A capped window minted on a boundary still lands inside the cap, not past it.</summary>
    [Fact]
    public void A_capped_window_minted_on_a_boundary_is_still_inside_the_cap()
    {
        var lifetime = new SignedUrlLifetime(TimeSpan.FromDays(365));
        DateTimeOffset now = new DateTimeOffset(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);

        Assert.Equal(
            now.Add(SignedUrlLifetime.Maximum),
            lifetime.ExpiryFrom(now));
    }

    /// <summary>
    /// The instant comes back as UTC whatever it was measured against, so a caller in another offset
    /// reports the same moment the token carries.
    /// </summary>
    [Fact]
    public void The_expiry_is_utc_whatever_offset_it_was_given()
    {
        var lifetime = new SignedUrlLifetime(TimeSpan.FromMinutes(20));
        DateTimeOffset instant = new DateTimeOffset(2026, 3, 14, 9, 30, 0, TimeSpan.FromHours(2));

        DateTimeOffset expiry = lifetime.ExpiryFrom(instant);

        Assert.Equal(TimeSpan.Zero, expiry.Offset);
        Assert.Equal(instant.ToUniversalTime().AddMinutes(20), expiry);
    }
}
