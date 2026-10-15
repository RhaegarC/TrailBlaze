namespace TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// A clock the test moves, so a rule about elapsed time is asserted without waiting it out.
/// </summary>
/// <remarks>
/// A delegation key is held for hours, and the container tier cannot spend them. Moving the clock
/// is what lets "a stale key is fetched again" be one test rather than a two-hour wait — and it
/// asserts the rule the implementation actually reads, which is the instant it asks for rather than
/// the one the machine happens to be at.
/// </remarks>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Moves the clock forward. Backwards is not offered: a clock that only advances is
    /// the one the rule is written for.</summary>
    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
