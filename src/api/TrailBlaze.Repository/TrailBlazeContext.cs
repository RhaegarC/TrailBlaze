using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TrailBlaze.Model.DatabaseEntity;
using System.Linq.Expressions;

namespace TrailBlaze.Repository
{
    public class TrailBlazeContext(DbContextOptions<TrailBlazeContext> options) : DbContext(options)
    {
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AuditLog>(entity =>
            {
                // An audit table is usually the fastest-growing table in a system, and it gets
                // read two ways: "what happened to this row" and "what happened around then".
                // Without an index each of those is a sequential scan of the whole history.
                entity.HasIndex(log => new { log.TableName, log.EntityId });
                entity.HasIndex(log => log.Timestamp);

                // SQL Server has no native JSON column type; the snapshots are JSON held in
                // nvarchar(max), which is what OPENJSON and JSON_VALUE read. That is the
                // provider's own convention for an unbounded string, so it is left to the
                // convention rather than restated here -- a `HasColumnType("nvarchar(max)")` would be
                // a no-op, and STANDARD §10 forbids a test that cannot go red guarding it.
            });

            ApplySoftDeleteFilter(modelBuilder);
        }

        /// <summary>
        /// Reads every <see cref="EntityBase"/> type through "not soft-deleted", so a row that
        /// <c>DeleteAsync</c> marked is genuinely gone from ordinary queries rather than merely
        /// flagged. Applied by convention across the model rather than named entity by entity,
        /// so a type added later inherits the filter instead of quietly returning deleted rows.
        /// </summary>
        /// <remarks>
        /// To see deleted rows anyway, the escape hatch is EF's own
        /// <c>IgnoreQueryFilters()</c> on the query — <c>Users.IgnoreQueryFilters()</c>. It is
        /// deliberately explicit: reading history back is a decision, not a default.
        /// </remarks>
        private static void ApplySoftDeleteFilter(ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!typeof(EntityBase).IsAssignableFrom(entityType.ClrType))
                {
                    continue;
                }

                // entity => entity.IsDeleted != true
                ParameterExpression entity = Expression.Parameter(entityType.ClrType, "entity");
                Expression notDeleted = Expression.NotEqual(
                    Expression.Property(entity, nameof(EntityBase.IsDeleted)),
                    Expression.Constant(true, typeof(bool?)));

                entityType.SetQueryFilter(Expression.Lambda(notDeleted, entity));
            }
        }
    }
}
