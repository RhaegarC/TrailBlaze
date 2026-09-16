namespace TrailBlaze.Repository.Test
{
    using Microsoft.EntityFrameworkCore;
    using TrailBlaze.Repository.Test.TestSupport;

    /// <summary>
    /// The model and the migration set, checked without a database.
    /// </summary>
    public sealed class PersistenceModelTests
    {
        /// <summary>
        /// The provider swap is the whole point of part of feature 01: the scaffold shipped
        /// against PostgreSQL. Asserting the provider name is what stops the swap from silently
        /// reverting the next time someone edits the registration.
        /// </summary>
        [Fact]
        public void The_context_is_configured_for_sql_server()
        {
            using var harness = new AuditHarness();

            Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", harness.Context.Database.ProviderName);
        }

        /// <summary>
        /// The migrations and the model must agree, or the schema a deployment applies is not
        /// the schema the code expects. This is the offline equivalent of "the migration set is
        /// current" — it compares the model to the snapshot and never connects.
        /// </summary>
        [Fact]
        public void The_model_matches_the_migration_snapshot()
        {
            using var harness = new AuditHarness();

            Assert.False(
                harness.Context.Database.HasPendingModelChanges(),
                "The model and the migration snapshot disagree. Regenerate with "
                + "`dotnet ef migrations add <name>` and commit the result.");
        }

        /// <summary>
        /// The audit snapshots were PostgreSQL's <c>jsonb</c> before the provider swap, and a
        /// column type is exactly the kind of thing that survives a swap unnoticed until a write
        /// fails. Both JSON columns must be string-typed and unbounded: <c>nvarchar(max)</c>
        /// holds the JSON, and a bounded type would truncate it silently.
        /// </summary>
        [Fact]
        public void The_audit_snapshots_are_unbounded_strings()
        {
            using var harness = new AuditHarness();

            var entity = harness.Context.Model
                .FindEntityType(typeof(TrailBlaze.Model.DatabaseEntity.AuditLog))!;

            string oldValues = entity.FindProperty(nameof(TrailBlaze.Model.DatabaseEntity.AuditLog.OldValues))!
                .GetColumnType();
            string newValues = entity.FindProperty(nameof(TrailBlaze.Model.DatabaseEntity.AuditLog.NewValues))!
                .GetColumnType();

            Assert.Equal("nvarchar(max)", oldValues);
            Assert.Equal("nvarchar(max)", newValues);
        }
    }
}
