namespace TrailBlaze.Service.Test;

using TrailBlaze.Model;

/// <summary>
/// When the delegation key already in hand may sign another URL, decided from a clock alone.
/// </summary>
/// <remarks>
/// <para>
/// Obtaining the key is a network call, so it is fetched once and held. But a SAS signed by a
/// delegation key is valid only inside that key's own lifetime, so "still alive" is not the
/// question — a key with two minutes left cannot sign the window <see cref="SignedUrlLifetime"/>
/// allows, and a URL minted from it would be refused by the account rather than by us.
/// </para>
/// <para>
/// It is asserted here, offline, because the alternative is a service call per case. The container
/// tier proves a key really is fetched once across many mints and really is fetched again when it
/// goes stale; this proves the arithmetic that decides it, which is where a wrong boundary would
/// live.
/// </para>
/// </remarks>
public sealed class DelegationKeyLifetimeTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 3, 14, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A key with more life left than the reserve is reused, so a listing does not fetch
    /// one key per URL.</summary>
    [Fact]
    public void A_key_with_more_than_the_reserve_left_is_reused()
    {
        Assert.True(DelegationKeyLifetime.ReusableAt(
            Noon, Noon + DelegationKeyLifetime.Reserve + TimeSpan.FromMinutes(1)));
    }

    /// <summary>
    /// A key with exactly the reserve left is not reused, and that boundary is the whole rule.
    /// </summary>
    /// <remarks>
    /// The reserve is the longest URL this application can sign, so a key holding exactly that much
    /// is a key whose URL would expire at the same instant the key does — the edge is not a margin,
    /// it is the point past which the signature and the key stop covering the same window.
    /// </remarks>
    [Fact]
    public void A_key_with_exactly_the_reserve_left_is_not_reused()
    {
        Assert.False(DelegationKeyLifetime.ReusableAt(Noon, Noon + DelegationKeyLifetime.Reserve));
    }

    /// <summary>A key inside the reserve is refused however much of it is left, including none.</summary>
    [Theory]
    [InlineData(59)]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_key_inside_the_reserve_is_not_reused(int minutesLeft)
    {
        Assert.False(DelegationKeyLifetime.ReusableAt(Noon, Noon.AddMinutes(minutesLeft)));
    }

    /// <summary>
    /// The reserve is the longest URL the application may sign, read from that cap rather than
    /// restated, so the two cannot drift apart in silence.
    /// </summary>
    /// <remarks>
    /// A reserve narrower than the cap would let a key sign a URL that outlives it; a wider one
    /// would fetch keys it did not need. Neither is visible without this assertion, because the
    /// failure is a refusal by the account at a customer's request rather than an error here.
    /// </remarks>
    [Fact]
    public void The_reserve_is_the_longest_url_the_application_can_sign()
    {
        Assert.Equal(SignedUrlLifetime.Maximum, DelegationKeyLifetime.Reserve);
    }

    /// <summary>
    /// A key asked for with the window the application asks for is usable the moment it arrives.
    /// </summary>
    /// <remarks>
    /// The other direction of the same mistake: a requested window no longer than the reserve would
    /// make every key stale on arrival, so every mint would fetch a new one and the cache would
    /// exist without ever holding anything.
    /// </remarks>
    [Fact]
    public void A_key_requested_for_the_configured_window_is_reusable_on_arrival()
    {
        Assert.True(DelegationKeyLifetime.Requested > DelegationKeyLifetime.Reserve);
        Assert.True(DelegationKeyLifetime.ReusableAt(Noon, Noon + DelegationKeyLifetime.Requested));
    }

    /// <summary>
    /// The rule is about the life a key has left, not about the clock it was fetched on.
    /// </summary>
    /// <remarks>
    /// One key, two readings of the clock: reused while it still covers the reserve, refused once
    /// the same key is nearer its end. A rule written against the fetch instant rather than the
    /// expiry would answer the same at both, which is what this separates.
    /// </remarks>
    [Fact]
    public void The_same_key_stops_being_reused_as_the_clock_moves()
    {
        DateTimeOffset expiresOn = Noon + DelegationKeyLifetime.Requested;

        Assert.True(DelegationKeyLifetime.ReusableAt(Noon, expiresOn));
        Assert.False(DelegationKeyLifetime.ReusableAt(
            Noon + DelegationKeyLifetime.Requested - DelegationKeyLifetime.Reserve, expiresOn));
    }
}
