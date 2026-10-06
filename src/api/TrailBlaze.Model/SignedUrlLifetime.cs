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

    /// <summary>How far the minting instant is rounded down before the window is added.</summary>
    /// <remarks>
    /// Taken from <see cref="Constant.MediaCache.MaxAgeSeconds"/> rather than chosen, because the two
    /// have to agree: that is how long a browser may reuse the bytes, and it keys them by the URL, so
    /// a URL that changes sooner than the copy it names lapses is a URL that misses a cache it could
    /// have hit. Every caller rounds by this, covers included — the granularity belongs to minting a
    /// URL, not to what the URL points at.
    /// </remarks>
    public static readonly TimeSpan Boundary = TimeSpan.FromSeconds(Constant.MediaCache.MaxAgeSeconds);

    /// <summary>When a URL minted at <paramref name="now"/> stops working.</summary>
    /// <remarks>
    /// The minting instant is rounded down to the whole boundary before the window is added, so two
    /// mints inside one boundary produce one string — a URL that moved between listings is a URL no
    /// browser could have cached. The result is then rounded to the whole second, which is the
    /// precision a SAS carries: an instant with a sub-second part would be truncated on the way into
    /// the token, and the caller would be told a moment the token does not hold.
    /// </remarks>
    public DateTimeOffset ExpiryFrom(DateTimeOffset now)
    {
        DateTimeOffset utc = now.ToUniversalTime();
        DateTimeOffset boundary = utc.AddTicks(-(utc.UtcTicks % Boundary.Ticks));
        DateTimeOffset expiry = boundary.Add(Applied);

        return new DateTimeOffset(
            expiry.UtcTicks - (expiry.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }
}
