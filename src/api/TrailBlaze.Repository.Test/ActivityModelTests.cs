namespace TrailBlaze.Repository.Test;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The shape of the <c>activities</c> table, read off the model with no database.
/// </summary>
/// <remarks>
/// The column list is the PRD's data model; what is asserted here is the part of it that the
/// model has to say out loud. A length, a nullability and a column type are all things EF will
/// happily guess at, and a guess is what the table gets.
/// </remarks>
public sealed class ActivityModelTests
{
    [Theory]
    [InlineData(nameof(Activity.Title), 200)]
    [InlineData(nameof(Activity.Location), 200)]
    [InlineData(nameof(Activity.Type), 16)]
    [InlineData(nameof(Activity.CoverImageBlobPath), 512)]
    public void A_column_is_bounded_to_its_documented_length(string propertyName, int expectedLength) =>
        Assert.Equal(expectedLength, PropertyOf(propertyName).GetMaxLength());

    /// <summary>
    /// The description is the one field with no bound, and that is a decision rather than an
    /// omission: it is the only free prose on the row, and the PRD gives it <c>nvarchar(max)</c>.
    /// A default length here would truncate entries the product invites people to write.
    /// </summary>
    [Fact]
    public void The_description_is_the_one_unbounded_column() =>
        Assert.Null(PropertyOf(nameof(Activity.Description)).GetMaxLength());

    /// <summary>
    /// A bare calendar date. Stored as <c>datetimeoffset</c> — the type every audit column takes —
    /// the day an entry falls on would depend on the reader's offset, and an entry logged on the
    /// 14th would read as the 13th for anyone west of the person who typed it.
    /// </summary>
    [Fact]
    public void The_activity_date_is_a_bare_calendar_date() =>
        Assert.Equal("date", PropertyOf(nameof(Activity.ActivityDate)).GetColumnType());

    /// <summary>
    /// Nullable against the model's wish, because "not yet uploaded" is a normal state rather
    /// than a missing value — see the avatar path, which is nullable for the same reason.
    /// </summary>
    [Fact]
    public void The_cover_path_is_nullable_because_an_entry_may_have_no_cover()
    {
        Assert.True(PropertyOf(nameof(Activity.Description)).IsNullable);
        Assert.True(PropertyOf(nameof(Activity.CoverImageBlobPath)).IsNullable);
    }

    /// <summary>
    /// The creator is the shared audit column <see cref="EntityBase.CreatedBy"/>, which every
    /// <see cref="EntityBase"/> already carries — so an entry has no creator column of its own.
    /// </summary>
    [Fact]
    public void The_creator_is_the_shared_audit_column() =>
        Assert.NotNull(PropertyOf(nameof(Activity.CreatedBy)));

    /// <summary>
    /// The closed set is enforced by the engine, not only by the code that writes it.
    /// </summary>
    /// <remarks>
    /// Asserted against a literal rather than against <c>Constant.ActivityType.All</c>, which is
    /// what the mapping composes the SQL from: a test that read the same set back would agree
    /// with the mapping however the set changed, and a fourth value would reach the table
    /// unnoticed.
    /// </remarks>
    [Fact]
    public void The_type_column_admits_only_the_closed_set()
    {
        IModel model = DesignTimeModel();
        IEntityType entityType = model.FindEntityType(typeof(Activity))!;
        ICheckConstraint constraint = Assert.Single(entityType.GetCheckConstraints());

        Assert.Equal("CK_Activities_Type", constraint.Name);
        Assert.Equal("[Type] IN ('Public', 'Shared', 'Private')", constraint.Sql);
    }

    /// <summary>
    /// The creator is a plain column, not a relationship.
    /// </summary>
    /// <remarks>
    /// The PRD draws a creator reference as an FK and the model declares no foreign keys at
    /// all (see the debt register). The two cannot both be true, and the difference is a
    /// behaviour rather than a diagram: with a relationship, EF would cascade a user's deletion
    /// into their entries, and nothing in the product asks for that. Which of the two is wrong
    /// is the debt item's question; that the model has no relationship is the fact an
    /// implementer builds on, so it is pinned here rather than left to be rediscovered.
    /// </remarks>
    [Fact]
    public void The_creator_is_a_column_and_not_a_relationship()
    {
        IEntityType entityType = DesignTimeModel().FindEntityType(typeof(Activity))!;

        Assert.Empty(entityType.GetForeignKeys());
        Assert.Empty(entityType.GetNavigations());
    }

    /// <summary>
    /// The soft-delete filter is applied by convention to every <see cref="EntityBase"/>, and
    /// this is the evidence that a type added to the model inherits it without being registered.
    /// </summary>
    [Fact]
    public void A_soft_deleted_entry_is_hidden_from_an_ordinary_query()
    {
        using var offline = new OfflineContext();

        string sql = offline.Context.Activities
            .Where(activity => activity.Title == "Ridge walk")
            .ToQueryString();

        int start = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsDeleted", start < 0 ? string.Empty : sql[start..]);
    }

    /// <summary>
    /// A paged read orders, filters and pages inside the statement rather than after it.
    /// </summary>
    /// <remarks>
    /// The filter has to reach the store with the page, not after it: a row the caller may not
    /// see that arrives in the page and is discarded there has already consumed one of the
    /// slots, so a page of ten can come back with fewer than ten visible rows while more exist.
    /// This pins EF's translation of the shape the repository composes; that the store then
    /// behaves that way is <c>ActivityPagingTests</c>'s claim.
    /// </remarks>
    [Fact]
    public void A_paged_query_filters_orders_and_pages_in_one_statement()
    {
        using var offline = new OfflineContext();

        string sql = offline.Context.Activities
            .Where(activity => activity.Type == "Public")
            .OrderByDescending(activity => activity.CreatedOn)
            .ThenByDescending(activity => activity.Id)
            .Skip(20)
            .Take(10)
            .ToQueryString();

        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);

        int page = sql.IndexOf("OFFSET", StringComparison.OrdinalIgnoreCase);

        Assert.True(page > sql.IndexOf("IsDeleted", StringComparison.OrdinalIgnoreCase));
        Assert.True(page > sql.IndexOf("[Type]", StringComparison.OrdinalIgnoreCase));
    }

    private static IProperty PropertyOf(string propertyName)
    {
        using var offline = new OfflineContext();

        IProperty? property = offline.Context.Model
            .FindEntityType(typeof(Activity))!
            .FindProperty(propertyName);

        Assert.True(
            property is not null,
            $"The model has no property 'Activity.{propertyName}'. Either it was never added, or "
            + "it is not mapped.");

        return property!;
    }

    /// <summary>The design-time model, which is where check constraints live: EF 10 keeps them
    /// out of the read-optimized runtime model.</summary>
    private static IModel DesignTimeModel()
    {
        using var offline = new OfflineContext();
        return offline.Context.GetService<IDesignTimeModel>().Model;
    }
}
