namespace TrailBlaze.Repository.Test
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using TrailBlaze.Model.DatabaseEntity;
    using TrailBlaze.Repository.Test.TestSupport;

    /// <summary>
    /// Audit stamping, offline. These assert what a caller can observe after a save: the history
    /// row and the audit columns. They never open a connection — see
    /// <see cref="AuditHarness"/>.
    /// </summary>
    public sealed class AuditTests
    {
        [Fact]
        public void An_insert_is_recorded_with_its_key_and_table()
        {
            using var harness = new AuditHarness();
            var user = new User { DisplayName = "Ada" };
            harness.Context.Users.Add(user);

            harness.Save();

            AuditLog log = harness.SingleAuditEntry();
            Assert.Equal("Users", log.TableName);
            Assert.Equal(user.Id, log.EntityId);
            Assert.Equal("Added", log.Action);
        }

        /// <summary>
        /// The key is on the entity before the save, which is why an insert can be audited with
        /// a real id instead of a placeholder — the property STANDARD §3 exists to protect.
        /// </summary>
        [Fact]
        public void A_new_entity_has_its_key_before_the_save()
        {
            using var harness = new AuditHarness();
            var user = new User();

            Assert.False(string.IsNullOrWhiteSpace(user.Id));
            Assert.True(Guid.TryParse(user.Id, out _));

            harness.Context.Users.Add(user);
            harness.Save();

            Assert.Equal(user.Id, harness.SingleAuditEntry().EntityId);
        }

        [Fact]
        public async Task The_async_save_path_is_audited_too()
        {
            using var harness = new AuditHarness();
            var user = new User { DisplayName = "Ada" };
            harness.Context.Users.Add(user);

            await harness.SaveAsync();

            AuditLog log = harness.SingleAuditEntry();
            Assert.Equal("Users", log.TableName);
            Assert.Equal(user.Id, log.EntityId);
        }

        [Fact]
        public void An_insert_stamps_the_audit_columns()
        {
            using var harness = new AuditHarness(FakeUserContext.Authenticated("oid-ada"));
            var user = new User();
            harness.Context.Users.Add(user);

            harness.Save();

            Assert.NotEqual(default, user.CreatedOn);
            Assert.Equal("oid-ada", user.CreatedBy);
            Assert.False(user.IsDeleted);
        }

        [Fact]
        public void Work_with_no_request_at_all_is_attributed_to_the_system()
        {
            using var harness = new AuditHarness(FakeUserContext.NoRequest());
            harness.Context.Users.Add(new User { DisplayName = "Ada" });

            harness.Save();

            Assert.Equal("system", harness.SingleAuditEntry().Actor);
        }

        [Fact]
        public void An_unauthenticated_request_is_recorded_as_anonymous()
        {
            using var harness = new AuditHarness(FakeUserContext.AnonymousRequest());
            harness.Context.Users.Add(new User { DisplayName = "Ada" });

            harness.Save();

            Assert.Equal("anonymous", harness.SingleAuditEntry().Actor);
        }

        [Fact]
        public void An_authenticated_request_is_recorded_against_the_object_id()
        {
            using var harness = new AuditHarness(FakeUserContext.Authenticated("oid-ada"));
            harness.Context.Users.Add(new User { DisplayName = "Ada" });

            harness.Save();

            Assert.Equal("oid-ada", harness.SingleAuditEntry().Actor);
        }

        /// <summary>
        /// A missing token and a background job are different problems, so they must not collapse
        /// into one value. This is the pairing that would catch someone "simplifying" the
        /// fallback to a single "unknown".
        /// </summary>
        [Fact]
        public void An_anonymous_request_and_a_missing_request_are_recorded_differently()
        {
            using (var anonymous = new AuditHarness(FakeUserContext.AnonymousRequest()))
            {
                anonymous.Context.Users.Add(new User());
                anonymous.Save();
                Assert.Equal("anonymous", anonymous.SingleAuditEntry().Actor);
            }

            using (var noRequest = new AuditHarness(FakeUserContext.NoRequest()))
            {
                noRequest.Context.Users.Add(new User());
                noRequest.Save();
                Assert.Equal("system", noRequest.SingleAuditEntry().Actor);
            }
        }

        [Fact]
        public void The_request_context_travels_with_the_audit_row()
        {
            using var harness = new AuditHarness(FakeUserContext.Authenticated("oid-ada"));
            harness.User.ActorName = "Ada Lovelace";
            harness.User.IpAddress = "203.0.113.7";
            harness.User.UserAgent = "TrailBlaze.Tests/1.0";
            harness.User.CorrelationId = "correlation-1";
            harness.Context.Users.Add(new User());

            harness.Save();

            AuditLog log = harness.SingleAuditEntry();
            Assert.Equal("Ada Lovelace", log.ActorName);
            Assert.Equal("203.0.113.7", log.IpAddress);
            Assert.Equal("TrailBlaze.Tests/1.0", log.UserAgent);
            Assert.Equal("correlation-1", log.CorrelationId);
        }

        /// <summary>
        /// The history row and the change it describes are staged on the same context, so they
        /// are committed in one transaction rather than leaving a change with no history.
        /// </summary>
        [Fact]
        public void The_history_row_is_staged_on_the_same_context_as_the_change()
        {
            using var harness = new AuditHarness();
            var user = new User();
            harness.Context.Users.Add(user);

            harness.Save();

            EntityEntry<AuditLog> staged = Assert.Single(harness.Context.ChangeTracker.Entries<AuditLog>());
            Assert.Equal(EntityState.Added, staged.State);
            Assert.Contains(user, harness.Context.ChangeTracker.Entries<User>().Select(e => e.Entity));
        }

        /// <summary>
        /// The update branch of the interceptor: it stamps the modification and records the row
        /// as "Modified".
        /// </summary>
        /// <remarks>
        /// The entity is attached as Modified rather than added-then-changed, because this tier
        /// never loads a row from a database and a failed save leaves the entity in the state it
        /// had, so there is no "existing row" to mutate.
        /// </remarks>
        [Fact]
        public void A_modified_entity_is_recorded_as_modified_and_stamped()
        {
            using var harness = new AuditHarness(FakeUserContext.Authenticated("oid-ada"));
            var user = new User { DisplayName = "Ada" };

            harness.Context.Users.Attach(user);
            harness.Context.Entry(user).State = EntityState.Modified;

            harness.Save();

            AuditLog log = harness.SingleAuditEntry();
            Assert.Equal("Modified", log.Action);
            Assert.Equal(user.Id, log.EntityId);
            Assert.Equal("oid-ada", user.LastModifiedBy);
            Assert.NotEqual(default, user.LastModifiedOn);
        }
    }
}
