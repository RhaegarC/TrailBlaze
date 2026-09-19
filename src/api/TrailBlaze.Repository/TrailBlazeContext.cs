namespace TrailBlaze.Repository;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using System.Linq.Expressions;

public class TrailBlazeContext(DbContextOptions<TrailBlazeContext> options) : DbContext(options)
{
    /// <summary>The name the engine knows the role's check constraint by. Named here rather
    /// than left to a convention so a migration that has to drop or recreate it can say which
    /// one it means.</summary>
    private const string RoleConstraintName = "CK_Users_Role";

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

        modelBuilder.Entity<User>(entity =>
        {
            // Lengths come from Constant.UserProfile, which is also where UserService reads
            // the number it shortens token claims to. An unbounded nvarchar(max) can reject
            // nothing, so this bound is what makes "shorten it to fit" a statement about
            // something -- and one copy of the number is what stops the two drifting.
            entity.Property(user => user.DisplayName).HasMaxLength(Constant.UserProfile.DisplayNameLength);
            entity.Property(user => user.Email).HasMaxLength(Constant.UserProfile.EmailLength);
            entity.Property(user => user.Description).HasMaxLength(Constant.UserProfile.DescriptionLength);
            entity.Property(user => user.AvatarBlobPath).HasMaxLength(Constant.UserProfile.AvatarBlobPathLength);

            // Role is an authorization input, so it carries more than a length. Non-nullable
            // with a default of User, for the reason the preferences below are non-nullable
            // and with a sharper consequence: a reader that had to decide what an absent role
            // means would be deciding what it grants.
            entity.Property(user => user.Role)
                .HasMaxLength(Constant.UserProfile.RoleLength)
                .HasDefaultValue(Constant.UserRole.User)
                .IsRequired();

            // And the closed set is enforced by the engine rather than by this app alone. A
            // column that accepts anything is one where 'Adminn' is stored successfully and
            // then recognized by nothing, and the failure surfaces as a person who cannot do
            // what they were told they could. Composed from Constant.UserRole.All so the SQL
            // cannot keep describing a set the constants have moved on from -- though the
            // constraint lives in the schema, so moving the set still needs a migration, which
            // the repository tier asserts the SQL of against a literal.
            entity.ToTable(table => table.HasCheckConstraint(
                RoleConstraintName,
                $"[{nameof(User.Role)}] IN "
                + $"({string.Join(", ", Constant.UserRole.All.Select(role => $"'{role}'"))})"));

            // Non-nullable with a database default, so a row inserted by a path that does
            // not know about preferences still lands on a usable value and every reader can
            // assume one is present rather than guessing what absence means.
            entity.Property(user => user.PreferredTheme)
                .HasMaxLength(Constant.UserProfile.PreferenceLength)
                .HasDefaultValue(Constant.UserPreference.DarkTheme)
                .IsRequired();

            entity.Property(user => user.PreferredLanguage)
                .HasMaxLength(Constant.UserProfile.PreferenceLength)
                .HasDefaultValue(Constant.UserPreference.English)
                .IsRequired();
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
