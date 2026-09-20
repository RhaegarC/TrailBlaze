namespace TrailBlaze.Repository;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using System.Linq.Expressions;

public class TrailBlazeContext(DbContextOptions<TrailBlazeContext> options) : DbContext(options)
{
    /// <summary>The name the engine knows the role's check constraint by.</summary>
    private const string RoleConstraintName = "CK_Users_Role";

    /// <summary>The name the engine knows the type's check constraint by.</summary>
    private const string TypeConstraintName = "CK_Activities_Type";

    /// <summary>The name the engine knows the media kind's check constraint by.</summary>
    private const string KindConstraintName = "CK_Media_Kind";

    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<User> Users { get; set; }

    /// <summary>Named without a plural, so the table is <c>Media</c> rather than <c>Medias</c>.</summary>
    public DbSet<Media> Media { get; set; }

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

        modelBuilder.Entity<Activity>(entity =>
        {
            entity.Property(activity => activity.Title).HasMaxLength(Constant.ActivityField.TitleLength);
            entity.Property(activity => activity.Location).HasMaxLength(Constant.ActivityField.LocationLength);

            // Required with no database default, unlike the user's preferences: a row whose
            // visibility nobody chose is a row whose reader would be deciding who may see it.
            entity.Property(activity => activity.Type)
                .HasMaxLength(Constant.ActivityField.TypeLength)
                .IsRequired();

            entity.Property(activity => activity.CoverImageBlobPath)
                .HasMaxLength(Constant.ActivityField.CoverImageBlobPathLength);

            // The closed set is enforced by the engine, not only by the service that writes it:
            // otherwise 'Friends' stores successfully and is then visible to nobody, or -- if a
            // reader treated an unknown value as the most permissive one -- to everybody.
            entity.ToTable(table => table.HasCheckConstraint(
                TypeConstraintName,
                $"[{nameof(Activity.Type)}] IN "
                + $"({string.Join(", ", Constant.ActivityType.All.Select(type => $"'{type}'"))})"));
        });

        modelBuilder.Entity<Media>(entity =>
        {
            // Not a relationship: the model declares no foreign keys, so an activity's deletion
            // leaves its media rows standing until the service removes them (Decision #27).
            entity.Property(media => media.ActivityId)
                .HasMaxLength(Constant.MediaField.ReferenceIdLength)
                .IsRequired();

            entity.Property(media => media.Kind)
                .HasMaxLength(Constant.MediaField.KindLength)
                .IsRequired();

            entity.Property(media => media.BlobPath)
                .HasMaxLength(Constant.MediaField.BlobPathLength)
                .IsRequired();

            entity.Property(media => media.ContentType)
                .HasMaxLength(Constant.MediaField.ContentTypeLength)
                .IsRequired();

            entity.Property(media => media.OriginalFileName)
                .HasMaxLength(Constant.MediaField.OriginalFileNameLength)
                .IsRequired();

            // Both reads of this table open on the activity — the listing asks for one activity's
            // items and the cap counts them — so the index is what keeps either from scanning every
            // media row in the database.
            entity.HasIndex(media => media.ActivityId);

            // The closed set is enforced by the engine as well as by the service that derives it,
            // for the same reason the activity's type is: a value outside the set is recognized by
            // nothing, and a reader would be guessing what it is about to serve.
            // Qualified below, because the DbSet property is named Media and shadows the type.
            entity.ToTable(table => table.HasCheckConstraint(
                KindConstraintName,
                $"[{nameof(TrailBlaze.Model.DatabaseEntity.Media.Kind)}] IN "
                + $"({string.Join(", ", Constant.MediaKind.All.Select(kind => $"'{kind}'"))})"));
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

            // Role is non-nullable with a default of User, because a reader deciding what an
            // absent role means would be deciding what it grants.
            entity.Property(user => user.Role)
                .HasMaxLength(Constant.UserProfile.RoleLength)
                .HasDefaultValue(Constant.UserRole.User)
                .IsRequired();

            // The closed set is enforced by the engine, not only by this app: otherwise
            // 'Adminn' stores successfully and is then recognized by nothing. Composed from
            // Constant.UserRole.All, though moving that set still needs a migration.
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
