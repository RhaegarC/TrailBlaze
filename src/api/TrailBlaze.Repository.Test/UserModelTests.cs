namespace TrailBlaze.Repository.Test;

using Microsoft.EntityFrameworkCore;
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
        using var harness = new AuditHarness();

        IProperty property = PropertyOf(harness, propertyName);

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
        using var harness = new AuditHarness();

        IProperty property = PropertyOf(harness, propertyName);

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
        using var harness = new AuditHarness();

        Assert.True(PropertyOf(harness, nameof(User.AvatarBlobPath)).IsNullable);
    }

    private static IProperty PropertyOf(AuditHarness harness, string propertyName)
    {
        IProperty? property = harness.Context.Model
            .FindEntityType(typeof(User))!
            .FindProperty(propertyName);

        Assert.True(
            property is not null,
            $"The model has no property 'User.{propertyName}'. Either it was never added, or "
            + "it is not mapped.");

        return property!;
    }
}
