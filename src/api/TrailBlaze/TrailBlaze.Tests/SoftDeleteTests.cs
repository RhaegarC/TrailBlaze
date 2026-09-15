using Microsoft.EntityFrameworkCore;
using TrailBlaze.Repository;
using TrailBlaze.Tests.TestSupport;

namespace TrailBlaze.Tests
{
    /// <summary>
    /// IM-13 applies the soft-delete filter by convention, to every <c>EntityBase</c> rather
    /// than entity by entity, so these tests assert the filter reaches the generated SQL and
    /// that the escape hatch still works.
    /// </summary>
    public class SoftDeleteTests
    {
        /// <summary>
        /// <c>ToQueryString()</c> builds the statement without running it, so this needs no
        /// database. Asserting on the presence of a WHERE clause is deliberate: the column
        /// list always mentions IsDeleted, so a substring check on the column name alone would
        /// pass even with the filter removed.
        /// </summary>
        [Fact]
        public void A_read_is_filtered_to_rows_that_are_not_deleted()
        {
            using var context = NewContext();

            string sql = context.Users.ToQueryString();

            Assert.Contains("WHERE", sql);
            Assert.Contains("IsDeleted", sql);
        }

        /// <summary>
        /// <c>IgnoreQueryFilters</c> is the documented way to read the deleted rows, which is
        /// what makes a soft delete recoverable rather than merely hidden.
        /// </summary>
        [Fact]
        public void IgnoreQueryFilters_drops_the_filter()
        {
            using var context = NewContext();

            string sql = context.Users.IgnoreQueryFilters().ToQueryString();

            Assert.DoesNotContain("WHERE", sql);
        }

        /// <summary>
        /// The filter applies by convention, and the convention is "is an EntityBase".
        /// <c>AuditLog</c> deliberately is not one — history is append-only, so there is no
        /// such thing as a deleted entry and nothing should be filtered from it.
        /// </summary>
        [Fact]
        public void The_audit_log_is_not_filtered()
        {
            using var context = NewContext();

            Assert.DoesNotContain("WHERE", context.AuditLogs.ToQueryString());
        }

        private static TrailBlazeContext NewContext() =>
            new(new DbContextOptionsBuilder<TrailBlazeContext>()
                .UseNpgsql(AuditHarness.UnreachableConnectionString)
                .Options);
    }
}
