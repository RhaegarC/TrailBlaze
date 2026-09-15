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

                // Npgsql maps string to text by default, which makes the snapshots
                // write-only. jsonb validates the JSON on the way in and stays queryable
                // afterwards -- the difference between a searchable history and a pile of
                // opaque blobs. Both columns hold JSON or null.
                entity.Property(log => log.OldValues).HasColumnType("jsonb");
                entity.Property(log => log.NewValues).HasColumnType("jsonb");
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
