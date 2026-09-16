namespace TrailBlaze.Repository.Test;

using Microsoft.EntityFrameworkCore;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The soft-delete filter, proved by inspecting generated SQL rather than by executing it —
/// there is no database in this tier (STANDARD §10).
/// </summary>
/// <remarks>
/// <para>
/// These assert on the <c>WHERE</c> clause and never on the whole statement. <c>IsDeleted</c>
/// is also a selected column, so a plain substring check over the full SQL passes whether or
/// not the filter was applied — a test that cannot fail. Narrowing to the predicate is what
/// makes the assertion mean something.
/// </para>
/// <para>
/// The first two are a pair for the same reason: one asserts the predicate is present, the
/// other asserts the opposite output for the same query with the filter bypassed, so a false
/// positive in either direction shows up.
/// </para>
/// </remarks>
public sealed class SoftDeleteFilterTests
{
    [Fact]
    public void An_ordinary_query_excludes_soft_deleted_rows()
    {
        using var harness = new AuditHarness();

        string predicate = WhereClause(harness.Context.Users
            .Where(user => user.DisplayName == "Ada")
            .ToQueryString());

        Assert.Contains("IsDeleted", predicate);
    }

    [Fact]
    public void The_predicate_is_absent_when_the_filter_is_bypassed_explicitly()
    {
        using var harness = new AuditHarness();

        string predicate = WhereClause(harness.Context.Users
            .IgnoreQueryFilters()
            .Where(user => user.DisplayName == "Ada")
            .ToQueryString());

        Assert.DoesNotContain("IsDeleted", predicate);
    }

    /// <summary>
    /// The filter is applied by convention across the model, so a type added later inherits
    /// it without being registered. <see cref="TrailBlaze.Model.DatabaseEntity.AuditLog"/>
    /// is the deliberate exception: history is append-only and has no deleted state to hide.
    /// </summary>
    [Fact]
    public void The_filter_is_applied_by_convention_and_history_is_the_exception()
    {
        using var harness = new AuditHarness();

        string usersPredicate = WhereClause(
            harness.Context.Users.Where(user => user.Id == "x").ToQueryString());
        string historyPredicate = WhereClause(
            harness.Context.AuditLogs.Where(log => log.Id == "x").ToQueryString());

        Assert.Contains("IsDeleted", usersPredicate);
        Assert.DoesNotContain("IsDeleted", historyPredicate);
    }

    /// <summary>
    /// Just the filter portion of the statement. Everything from <c>WHERE</c> on, so the
    /// selected-column list cannot satisfy an assertion about the predicate.
    /// </summary>
    private static string WhereClause(string sql)
    {
        int start = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        return start < 0 ? string.Empty : sql[start..];
    }
}
