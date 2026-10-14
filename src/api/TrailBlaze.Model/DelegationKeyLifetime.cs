namespace TrailBlaze.Model;

/// <summary>
/// How long a user delegation key is asked for, and how much of one must remain before it may sign
/// another URL.
/// </summary>
/// <remarks>
/// <para>
/// Obtaining the key is a network round trip, so it is fetched once and held. But a SAS signed by a
/// delegation key is valid only inside that key's own lifetime, so the question is never "is the
/// key still alive": a key with two minutes left cannot sign the window
/// <see cref="SignedUrlLifetime.Maximum"/> allows, and a URL minted from it is refused by the
/// account rather than by us.
/// </para>
/// <para>
/// That is why the reuse rule is a reserve rather than an expiry. A key is held only while it still
/// covers the longest URL this application can sign, whatever the URL being signed right now asks
/// for — a rule written against the request would let a two-minute window through on a key that had
/// no chance of covering a sixty-minute one.
/// </para>
/// </remarks>
public static class DelegationKeyLifetime
{
    /// <summary>How long a key is asked for. The emulator returns exactly what it is asked for,
    /// so this is the window the account will honour.</summary>
    public static readonly TimeSpan Requested = TimeSpan.FromHours(2);

    /// <summary>How much life a key must still have to sign again — read from the cap it exists to
    /// cover rather than restated, so the two cannot drift apart in silence.</summary>
    public static readonly TimeSpan Reserve = SignedUrlLifetime.Maximum;

    /// <summary>Whether a key expiring at <paramref name="expiresOn"/> may sign another URL at
    /// <paramref name="now"/>.</summary>
    public static bool ReusableAt(DateTimeOffset now, DateTimeOffset expiresOn) =>
        now + Reserve < expiresOn;
}
