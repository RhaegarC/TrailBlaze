namespace TrailBlaze.Model;

/// <summary>
/// The window a signed read URL is given, and the instant it ends.
/// </summary>
/// <remarks>
/// <para>
/// One home for the window, because the window is the whole control: a SAS is a bearer token, so
/// anybody holding the string reads the blob until it lapses. Every caller that mints one — the media
/// route and the private-cover path — takes its lifetime from here, so the cap cannot be raised by
/// configuration and neither caller can round differently.
/// </para>
/// <para>
/// The expiry is an instant rather than a duration because that is what makes the instant signed and
/// the instant reported the same one. The storage layer signs exactly what it is handed, so a caller
/// that tells a client "valid until <c>T</c>" knows the token carries <c>T</c> and not a moment the
/// storage layer chose.
/// </para>
/// </remarks>
/// <param name="Configured">What configuration asked for, which is not necessarily what is applied.</param>
public sealed record SignedUrlLifetime(TimeSpan Configured)
{
    /// <summary>Applied when nothing is configured, and to a configured value that is not positive.</summary>
    public static readonly TimeSpan Default = TimeSpan.FromMinutes(15);

    /// <summary>The longest window any signed URL may be given, whatever configuration says.</summary>
    public static readonly TimeSpan Maximum = TimeSpan.FromMinutes(60);

    /// <summary>The window actually applied, which is <see cref="Configured"/> inside the cap.</summary>
    public TimeSpan Applied =>
        Configured <= TimeSpan.Zero ? Default
        : Configured > Maximum ? Maximum
        : Configured;

    /// <summary>When a URL minted at <paramref name="now"/> stops working.</summary>
    /// <remarks>
    /// Rounded down to the whole second, which is the precision a SAS carries: an instant with a
    /// sub-second part would be truncated on the way into the token, and the caller would then be
    /// told a moment the token does not hold.
    /// </remarks>
    public DateTimeOffset ExpiryFrom(DateTimeOffset now)
    {
        DateTimeOffset utc = now.ToUniversalTime().Add(Applied);

        return new DateTimeOffset(utc.UtcTicks - (utc.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }
}
