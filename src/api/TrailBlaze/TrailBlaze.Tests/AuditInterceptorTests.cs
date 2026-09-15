using Microsoft.EntityFrameworkCore;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Tests.TestSupport;

namespace TrailBlaze.Tests
{
    /// <summary>
    /// The interceptor runs on both save paths (IM-11) and keys are application-assigned
    /// (IM-12), which together mean an insert is auditable and traceable. These tests cover
    /// both, plus the actor fallbacks.
    /// </summary>
    public class AuditInterceptorTests
    {
        [Fact]
        public void An_insert_is_recorded_with_its_key_and_table()
        {
            using var harness = new AuditHarness();
            var user = new User { DisplayName = "Ada" };
            harness.Context.Users.Add(user);

            harness.Save();

            var log = harness.SingleEntry();
            Assert.Equal("Users", log.TableName);
            Assert.Equal("Added", log.Action);
            Assert.Equal(user.Id, log.EntityId);
        }

        /// <summary>
        /// The counterpart to IM-14: an insert has no before-state, so <c>OldValues</c> stays
        /// null while <c>NewValues</c> carries the row as it was written.
        /// </summary>
        [Fact]
        public void An_insert_has_new_values_and_no_old_values()
        {
            using var harness = new AuditHarness();
            harness.Context.Users.Add(new User { DisplayName = "Ada" });

            harness.Save();

            var log = harness.SingleEntry();
            Assert.Null(log.OldValues);
            Assert.Contains("Ada", log.NewValues);
        }

        [Fact]
        public void An_update_records_both_sides_of_the_change()
        {
            using var harness = new AuditHarness();
            var user = new User { Id = "user-1", DisplayName = "before" };
            harness.Context.Attach(user);
            harness.Context.Entry(user).Property(u => u.DisplayName).CurrentValue = "after";
            harness.Context.Entry(user).Property(u => u.DisplayName).IsModified = true;

            harness.Save();

            var log = harness.SingleEntry();
            Assert.Equal("Modified", log.Action);
            Assert.Contains("before", log.OldValues);
            Assert.Contains("after", log.NewValues);
            Assert.Contains(nameof(User.DisplayName), log.ChangedColumns);
        }

        /// <summary>
        /// A delete has no after-state. A soft delete is a modification and is recorded as
        /// one — the vocabulary does not distinguish them, which is a known open question
        /// rather than an accident of this test.
        /// </summary>
        [Fact]
        public void A_delete_has_old_values_and_no_new_values()
        {
            using var harness = new AuditHarness();
            var user = new User { Id = "user-1", DisplayName = "gone" };
            harness.Context.Attach(user);
            harness.Context.Remove(user);

            harness.Save();

            var log = harness.SingleEntry();
            Assert.Equal("Deleted", log.Action);
            Assert.Contains("gone", log.OldValues);
            Assert.Null(log.NewValues);
        }

        /// <summary>
        /// IM-11: <c>SaveChanges()</c> is a separate override from <c>SaveChangesAsync()</c>,
        /// so auditing one says nothing about the other. This is the regression guard for the
        /// synchronous path having been silent.
        /// </summary>
        [Fact]
        public async Task The_async_save_path_is_audited_too()
        {
            using var harness = new AuditHarness();
            harness.Context.Users.Add(new User { DisplayName = "Ada" });

            await harness.SaveAsync();

            Assert.Equal("Users", harness.SingleEntry().TableName);
        }

        [Fact]
        public void The_actor_is_the_authenticated_object_id()
        {
            using var harness = new AuditHarness(new FakeUserContext
            {
                EntraObjectId = "oid-123",
                HasActiveRequest = true,
            });

            harness.Context.Users.Add(new User());
            harness.Save();

            Assert.Equal("oid-123", harness.SingleEntry().Actor);
        }

        /// <summary>
        /// A request that arrived without a token and background work that had no request at
        /// all are different situations, and the audit trail says which one happened rather
        /// than collapsing both into "unknown".
        /// </summary>
        [Fact]
        public void An_unauthenticated_request_is_recorded_as_anonymous()
        {
            using var harness = new AuditHarness(new FakeUserContext { HasActiveRequest = true });

            harness.Context.Users.Add(new User());
            harness.Save();

            Assert.Equal("anonymous", harness.SingleEntry().Actor);
        }

        [Fact]
        public void Work_with_no_request_in_flight_is_recorded_as_system()
        {
            using var harness = new AuditHarness(new FakeUserContext { HasActiveRequest = false });

            harness.Context.Users.Add(new User());
            harness.Save();

            Assert.Equal("system", harness.SingleEntry().Actor);
        }

        [Fact]
        public void Request_metadata_is_carried_onto_the_entry()
        {
            using var harness = new AuditHarness(new FakeUserContext
            {
                EntraObjectId = "oid-123",
                HasActiveRequest = true,
                ActorName = "Ada Lovelace",
                IpAddress = "10.0.0.1",
                UserAgent = "xunit",
                CorrelationId = "trace-1",
            });

            harness.Context.Users.Add(new User());
            harness.Save();

            var log = harness.SingleEntry();
            Assert.Equal("Ada Lovelace", log.ActorName);
            Assert.Equal("10.0.0.1", log.IpAddress);
            Assert.Equal("xunit", log.UserAgent);
            Assert.Equal("trace-1", log.CorrelationId);
        }

        /// <summary>
        /// Audit entries are staged on the same context, and the interceptor skips them. Without
        /// that guard each save would audit the audit entries it just wrote, and every save
        /// would grow the batch without bound.
        /// </summary>
        [Fact]
        public void Writing_an_audit_entry_does_not_audit_the_audit_entry()
        {
            using var harness = new AuditHarness();
            harness.Context.Users.Add(new User());

            harness.Save();

            Assert.Single(harness.Recorded());
        }
    }
}
