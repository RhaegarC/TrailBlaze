namespace TrailBlaze.Repository.Test;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Repository.Test.TestSupport;

/// <summary>
/// The shape of the <c>Media</c> table, read off the model with no database.
/// </summary>
/// <remarks>
/// The column list is the PRD's data model; what is asserted here is the part of it the model has to
/// say out loud. A length, a nullability and a column type are all things EF will happily guess at,
/// and a guess is what the table gets.
/// </remarks>
public sealed class MediaModelTests
{
    [Theory]
    [InlineData(nameof(Media.ActivityId), 128)]
    [InlineData(nameof(Media.UploadedByUserId), 128)]
    [InlineData(nameof(Media.Kind), 16)]
    [InlineData(nameof(Media.BlobPath), 512)]
    [InlineData(nameof(Media.ContentType), 128)]
    [InlineData(nameof(Media.OriginalFileName), 260)]
    public void A_column_is_bounded_to_its_documented_length(string propertyName, int expectedLength) =>
        Assert.Equal(expectedLength, PropertyOf(propertyName).GetMaxLength());

    /// <summary>
    /// Every column but the audit pair is required, including the file name: an item whose name is
    /// missing is a client that sent none, which is stored as empty rather than as null, so a reader
    /// never has to decide what a missing name means.
    /// </summary>
    [Theory]
    [InlineData(nameof(Media.ActivityId))]
    [InlineData(nameof(Media.UploadedByUserId))]
    [InlineData(nameof(Media.Kind))]
    [InlineData(nameof(Media.BlobPath))]
    [InlineData(nameof(Media.ContentType))]
    [InlineData(nameof(Media.OriginalFileName))]
    public void A_required_column_is_non_nullable(string propertyName) =>
        Assert.False(PropertyOf(propertyName).IsNullable);

    /// <summary>
    /// A 200 MB video does not fit in an <c>int</c>, and the column EF infers for a
    /// <c>long</c> is <c>bigint</c> — asserted because an <c>int</c> mapping would overflow at
    /// 2 GB and, worse, would silently truncate a size a client reported.
    /// </summary>
    [Fact]
    public void The_size_is_a_64_bit_number() =>
        Assert.Equal("bigint", PropertyOf(nameof(Media.SizeBytes)).GetColumnType());

    /// <summary>
    /// The closed set is enforced by the engine as well as by the service that derives the kind.
    /// </summary>
    /// <remarks>
    /// Asserted against the literal SQL rather than against <c>Constant.MediaKind.All</c>, which is
    /// what the mapping composes it from: a test that read the same set back would agree with the
    /// mapping however the set changed, and a third kind would reach the table unnoticed.
    /// </remarks>
    [Fact]
    public void The_kind_column_admits_only_the_closed_set()
    {
        IModel model = DesignTimeModel();
        IEntityType entityType = model.FindEntityType(typeof(Media))!;
        ICheckConstraint constraint = Assert.Single(entityType.GetCheckConstraints());

        Assert.Equal("CK_Media_Kind", constraint.Name);
        Assert.Equal("[Kind] IN ('Image', 'Video')", constraint.Sql);
    }

    /// <summary>
    /// The table is named for the type and not for a plural, which is the one place this model
    /// departs from the DbSet-name convention: the PRD's data model calls it <c>Media</c>, and
    /// <c>Medias</c> would be a name no reader has seen before.
    /// </summary>
    [Fact]
    public void The_table_keeps_the_name_the_data_model_gives_it() =>
        Assert.Equal("Media", DesignTimeModel().FindEntityType(typeof(Media))!.GetTableName());

    /// <summary>
    /// Both of the item's references are plain columns, not relationships.
    /// </summary>
    /// <remarks>
    /// The PRD draws <c>ActivityId</c> and <c>UploadedByUserId</c> as FKs and the model declares no
    /// foreign keys at all (see the debt register). The difference is a behaviour rather than a
    /// diagram: with a relationship, EF would cascade an activity's deletion into its media and a
    /// user's deletion into everything they contributed, and neither is what the product asks for —
    /// <c>ActivityService</c> removes the media itself, and a user is never deleted at all.
    /// </remarks>
    [Fact]
    public void Both_references_are_columns_and_not_relationships()
    {
        IEntityType entityType = DesignTimeModel().FindEntityType(typeof(Media))!;

        Assert.Empty(entityType.GetForeignKeys());
        Assert.Empty(entityType.GetNavigations());
    }

    /// <summary>
    /// The activity is indexed, because both reads of this table open on it: the listing asks for one
    /// activity's items and the cap counts them.
    /// </summary>
    [Fact]
    public void The_activity_is_indexed()
    {
        IEntityType entityType = DesignTimeModel().FindEntityType(typeof(Media))!;
        IIndex index = Assert.Single(entityType.GetIndexes());

        Assert.Equal([nameof(Media.ActivityId)], index.Properties.Select(property => property.Name));
    }

    /// <summary>
    /// The soft-delete filter is applied by convention to every <see cref="EntityBase"/>, and this is
    /// the evidence that the new type inherits it without being registered anywhere.
    /// </summary>
    [Fact]
    public void A_soft_deleted_item_is_hidden_from_an_ordinary_query()
    {
        using var offline = new OfflineContext();

        string sql = offline.Context.Media
            .Where(item => item.ActivityId == "the-activity")
            .ToQueryString();

        int start = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsDeleted", start < 0 ? string.Empty : sql[start..]);
    }

    private static IProperty PropertyOf(string propertyName)
    {
        using var offline = new OfflineContext();

        IProperty? property = offline.Context.Model
            .FindEntityType(typeof(Media))!
            .FindProperty(propertyName);

        Assert.True(
            property is not null,
            $"The model has no property 'Media.{propertyName}'. Either it was never added, or it is "
            + "not mapped.");

        return property!;
    }

    /// <summary>The design-time model, which is where check constraints live: EF 10 keeps them out
    /// of the read-optimized runtime model.</summary>
    private static IModel DesignTimeModel()
    {
        using var offline = new OfflineContext();
        return offline.Context.GetService<IDesignTimeModel>().Model;
    }
}
