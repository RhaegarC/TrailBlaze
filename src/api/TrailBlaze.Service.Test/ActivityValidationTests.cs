namespace TrailBlaze.Service.Test;

using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.Activity;

/// <summary>
/// The rules an activity's fields must satisfy, and the form they take once they do.
/// </summary>
/// <remarks>
/// All of it decided from the request alone, which is what lets it run here rather than in a
/// container tier. The normalisation rules live beside the validation rules because both answer
/// the same question — what is stored — and splitting them would be two chances to disagree.
/// </remarks>
public sealed class ActivityValidationTests
{
    private const string TitleField = nameof(CreateActivityRequest.Title);
    private const string LocationField = nameof(CreateActivityRequest.Location);
    private const string DateField = nameof(CreateActivityRequest.ActivityDate);
    private const string TypeField = nameof(CreateActivityRequest.Type);

    private static readonly IActivityValidationService Validation = new ActivityValidationService();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_title_that_is_not_there_is_refused(string? title) =>
        Assert.Contains(TitleField, Validation.Validate(Valid() with { Title = title }).Keys);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t ")]
    public void A_location_that_is_not_there_is_refused(string? location) =>
        Assert.Contains(LocationField, Validation.Validate(Valid() with { Location = location }).Keys);

    /// <summary>The cap is inclusive: a value exactly at it is what the column admits.</summary>
    [Fact]
    public void A_title_at_the_cap_is_accepted() =>
        Assert.Empty(Validation.Validate(Valid() with { Title = Filled(Constant.ActivityField.TitleLength) }));

    [Fact]
    public void A_title_one_character_over_the_cap_is_refused() =>
        Assert.Contains(
            TitleField,
            Validation.Validate(Valid() with { Title = Filled(Constant.ActivityField.TitleLength + 1) }).Keys);

    [Fact]
    public void A_location_at_the_cap_is_accepted() =>
        Assert.Empty(Validation.Validate(Valid() with { Location = Filled(Constant.ActivityField.LocationLength) }));

    [Fact]
    public void A_location_one_character_over_the_cap_is_refused() =>
        Assert.Contains(
            LocationField,
            Validation.Validate(Valid() with { Location = Filled(Constant.ActivityField.LocationLength + 1) }).Keys);

    [Fact]
    public void A_missing_activity_date_is_refused() =>
        Assert.Contains(DateField, Validation.Validate(Valid() with { ActivityDate = null }).Keys);

    /// <summary>Every problem is reported, not only the first, so a client does not need one
    /// round trip per mistake.</summary>
    [Fact]
    public void Every_bad_field_is_reported_at_once()
    {
        IReadOnlyDictionary<string, string[]> errors =
            Validation.Validate(new CreateActivityRequest { Title = " ", Location = " ", Type = "Friends" });

        Assert.Contains(TitleField, errors.Keys);
        Assert.Contains(LocationField, errors.Keys);
        Assert.Contains(DateField, errors.Keys);
        Assert.Contains(TypeField, errors.Keys);
    }

    [Theory]
    [InlineData("Public")]
    [InlineData("public")]
    [InlineData("SHARED")]
    [InlineData("Private")]
    public void A_type_from_the_closed_set_is_accepted(string type) =>
        Assert.Empty(Validation.Validate(Valid() with { Type = type }));

    /// <summary>An absent type is a caller who supplied none, which the default answers — not
    /// an invalid value.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_absent_type_is_accepted(string? type) =>
        Assert.Empty(Validation.Validate(Valid() with { Type = type }));

    [Theory]
    [InlineData("Friends")]
    [InlineData("Publicc")]
    [InlineData("private entry")]
    public void A_type_outside_the_closed_set_is_refused(string type) =>
        Assert.Contains(TypeField, Validation.Validate(Valid() with { Type = type }).Keys);

    /// <summary>Surrounding whitespace is not a different value. It is trimmed before the
    /// comparison, so a client that pads a field is not refused for it.</summary>
    [Fact]
    public void A_padded_type_is_still_the_type_it_names() =>
        Assert.Empty(Validation.Validate(Valid() with { Type = "  shared  " }));

    [Fact]
    public void An_absent_description_is_stored_as_null()
    {
        Assert.Null(ActivityDraft.From(Valid() with { Description = "   " }).Description);
        Assert.Null(ActivityDraft.From(Valid() with { Description = null }).Description);
    }

    [Fact]
    public void The_surrounding_whitespace_is_not_stored()
    {
        ActivityDraft draft = ActivityDraft.From(Valid() with { Title = "  Ridge walk  ", Location = " North  " });

        Assert.Equal("Ridge walk", draft.Title);
        Assert.Equal("North", draft.Location);
    }

    /// <summary>A reader never has to normalise the column: what a caller typed with the wrong
    /// case is stored in the one spelling the closed set names.</summary>
    [Fact]
    public void The_stored_type_is_the_canonical_one()
    {
        Assert.Equal(Constant.ActivityType.Public, ActivityDraft.From(Valid() with { Type = "public" }).Type);
        Assert.Equal(Constant.ActivityType.Shared, ActivityDraft.From(Valid() with { Type = " shared " }).Type);
        Assert.Equal(Constant.ActivityType.Private, ActivityDraft.From(Valid() with { Type = "PRIVATE" }).Type);
    }

    [Fact]
    public void An_absent_type_is_stored_as_the_default() =>
        Assert.Equal(Constant.ActivityType.Default, ActivityDraft.From(Valid() with { Type = null }).Type);

    private static CreateActivityRequest Valid() => new()
    {
        Title = "Ridge walk",
        Location = "North ridge",
        ActivityDate = new DateOnly(2026, 3, 14),
    };

    private static string Filled(int length) => new('a', length);
}
