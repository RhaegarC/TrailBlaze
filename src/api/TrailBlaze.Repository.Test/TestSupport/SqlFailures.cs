namespace TrailBlaze.Repository.Test.TestSupport;

using Microsoft.Data.SqlClient;

/// <summary>
/// Reading a provider failure out of whatever the EF layer hands back.
/// </summary>
/// <remarks>
/// EF is free to wrap a <see cref="SqlException"/> on its way out of <c>SaveChanges</c> or
/// <c>MigrateAsync</c>, and it does: the engine's error arrives as the inner exception of a
/// <c>DbUpdateException</c>, not as the thrown type. Pinning the wrapper in a test would make
/// it a test of EF's exception plumbing rather than of the engine's answer, which is the thing
/// actually worth asserting. Both callers walk the same chain for that reason, so the walk
/// lives once.
/// </remarks>
internal static class SqlFailures
{
    /// <summary>
    /// The first <see cref="SqlException"/> in an exception's inner chain, or <c>null</c> when
    /// there is none — including when <paramref name="exception"/> is itself null.
    /// </summary>
    /// <remarks>
    /// Returning null rather than throwing is deliberate: "there was no SqlException" is a
    /// real and informative outcome, and callers assert on it with a message naming what they
    /// expected instead. An <c>AggregateException</c> is not unwrapped, because neither EF path
    /// produces one.
    /// </remarks>
    public static SqlException? Find(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql)
            {
                return sql;
            }
        }

        return null;
    }

    /// <summary>
    /// The engine codes for a duplicate key, which is two numbers rather than one.
    /// </summary>
    /// <remarks>
    /// <b>2627</b> is a PRIMARY KEY or UNIQUE <em>constraint</em> violation; <b>2601</b> is a
    /// duplicate row in a UNIQUE <em>index</em>. Which one arrives depends on whether the
    /// uniqueness was declared as a constraint or as an index, not on how wrong the write was,
    /// so a catch that recognises only one of them silently stops catching the day a schema
    /// change moves a constraint to an index. They are listed together for the same reason the
    /// migration tests list both truncation codes: the claim that holds either way is "the
    /// engine refused the duplicate", and that is the claim worth pinning.
    /// </remarks>
    public static readonly int[] DuplicateKey = [2627, 2601];
}
