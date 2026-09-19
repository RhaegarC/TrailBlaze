namespace TrailBlaze.Repository.Test;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The shape of the <c>users</c> table, read off the model with no database.
/// </summary>
/// <remarks>
/// The columns were unbounded until feature 02: EF's convention gives every <c>string</c> an
/// <c>nvarchar(max)</c>, and nothing here had ever said otherwise. That is not a neutral
/// default. An unbounded column cannot reject anything, so a token claim longer than the
/// screen can render is stored in full, and the profile screen that reads it back is the
/// first thing to notice. Bounding them is what makes "truncate to the column length" a
/// statement about something.
///
/// <para>These assertions read <c>IModel</c> metadata, so they prove what the <em>model</em>
/// says. Whether SQL Server accepts the accompanying <c>ALTER COLUMN</c> against a table that
/// already holds rows is not provable here — that belongs to the deployment that applies it,
/// and feature 11.</para>
/// </remarks>
public sealed class UserModelTests
{
    /// <summary>
    /// Each profile column carries the length the PRD's data-model row gives it. A length of
    /// <c>null</c> here means the column is <c>nvarchar(max)</c> and the truncation rule has
    /// nothing to truncate to.
    /// </summary>
    [Theory]
    [InlineData(nameof(User.DisplayName), 200)]
    [InlineData(nameof(User.Email), 320)]
    [InlineData(nameof(User.Description), 500)]
    [InlineData(nameof(User.AvatarBlobPath), 512)]
    [InlineData(nameof(User.Role), 16)]
    [InlineData(nameof(User.PreferredTheme), 16)]
    [InlineData(nameof(User.PreferredLanguage), 16)]
    public void A_profile_column_is_bounded_to_its_documented_length(
        string propertyName, int expectedLength)
    {
        using var offline = new OfflineContext();

        IProperty property = PropertyOf(offline, propertyName);

        Assert.Equal(expectedLength, property.GetMaxLength());
    }

    /// <summary>
    /// The two presentation preferences are non-nullable and defaulted, so no client ever has
    /// to decide what an absent preference means. A nullable column would push that decision
    /// onto every reader — the profile screen, the export, and whatever renders the theme —
    /// and each would have to guess the same answer independently.
    /// </summary>
    [Theory]
    [InlineData(nameof(User.PreferredTheme), "Dark")]
    [InlineData(nameof(User.PreferredLanguage), "en")]
    public void A_preference_column_is_non_nullable_and_defaulted(
        string propertyName, string expectedDefault)
    {
        using var offline = new OfflineContext();

        IProperty property = PropertyOf(offline, propertyName);

        Assert.False(property.IsNullable);
        Assert.Equal(expectedDefault, property.GetDefaultValue());
    }

    /// <summary>
    /// The avatar path is deliberately nullable: a user who has never uploaded one has no
    /// path, and that is a normal state rather than a missing value. Being nullable is what
    /// lets "remove an avatar you do not have" be a no-op instead of a special case.
    /// </summary>
    [Fact]
    public void The_avatar_path_is_nullable_because_most_users_have_no_avatar()
    {
        using var offline = new OfflineContext();

        Assert.True(PropertyOf(offline, nameof(User.AvatarBlobPath)).IsNullable);
    }

    /// <summary>
    /// The role is non-nullable and defaults to <c>User</c>.
    /// </summary>
    /// <remarks>
    /// Non-nullable, so no reader has to decide what an absent role means — the same argument
    /// the preferences carry, applied to the one column where guessing is a security decision.
    /// <c>User</c> rather than <c>Admin</c> is least privilege as a default: a row inserted by
    /// a path that never mentions the column, or one that predates it, lands on the powerless
    /// value. Declaring it here is the column's half of that; the migration's backfill is the
    /// other, and it defaults to the same value for the same reason.
    /// </remarks>
    [Fact]
    public void The_role_column_is_non_nullable_and_defaults_to_user()
    {
        using var offline = new OfflineContext();

        IProperty property = PropertyOf(offline, nameof(User.Role));

        Assert.False(property.IsNullable);
        Assert.Equal("User", property.GetDefaultValue());
    }

    /// <summary>
    /// The role is limited to the closed set by the engine, not only by the code that writes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asserted against a literal rather than against <c>Constant.UserRole.All</c>, which is
    /// what the mapping composes the SQL from. A test that read the same constant back would
    /// agree with the mapping however the set changed, and would pass over a column still
    /// admitting a value the constants had dropped; the literal fails there, and a change to
    /// the set then has to be matched by a migration — which is true, because the constraint is
    /// in the schema.
    /// </para>
    /// <para>
    /// This is the model's declaration of it. That SQL Server actually refuses a third value,
    /// and that the tightening applies to a table that already holds rows, are properties of
    /// the migration rather than of the model, and belong to the container tier —
    /// <see cref="UserRoleMigrationTests"/>.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_role_column_admits_only_the_closed_set()
    {
        using var offline = new OfflineContext();

        // The design-time model, not Context.Model. EF 10 keeps check constraints out of the
        // read-optimized runtime model — asking the wrong one throws rather than answering, so
        // this is not a subtle wrong answer but it is an easy line to copy from the tests
        // above, where Context.Model is exactly right.
        IModel model = offline.Context.GetService<IDesignTimeModel>().Model;

        IEntityType entityType = model.FindEntityType(typeof(User))!;
        ICheckConstraint constraint = Assert.Single(entityType.GetCheckConstraints());

        Assert.Equal("CK_Users_Role", constraint.Name);
        Assert.Equal("[Role] IN ('User', 'Admin')", constraint.Sql);
    }

    private static IProperty PropertyOf(OfflineContext offline, string propertyName)
    {
        IProperty? property = offline.Context.Model
            .FindEntityType(typeof(User))!
            .FindProperty(propertyName);

        Assert.True(
            property is not null,
            $"The model has no property 'User.{propertyName}'. Either it was never added, or "
            + "it is not mapped.");

        return property!;
    }
}
