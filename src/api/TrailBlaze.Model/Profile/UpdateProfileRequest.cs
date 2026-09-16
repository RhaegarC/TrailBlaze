namespace TrailBlaze.Model.Profile;

/// <summary>
/// What a caller may change about their own profile.
/// </summary>
/// <remarks>
/// <para>
/// <b>The properties this type does not have are the security control.</b> <c>Id</c>,
/// <c>Email</c> and <c>Role</c> are deliberately absent, and their absence — not a check
/// somewhere — is what stops a caller setting them. <c>Role</c> is the one that matters: a
/// caller who could write it would promote themselves to admin, which is both silent and
/// permanent, and it would defeat feature 03's seeding outright, because the seeded admin
/// would be one of any number of admins rather than the only one. <c>Id</c> would let a
/// caller address another row, and <c>Email</c> is a token claim this app does not take from
/// user input.
/// </para>
/// <para>
/// Adding a property here is therefore a decision about privilege, not a schema convenience.
/// A future field that happens to be harmless still belongs behind that question.
/// </para>
/// <para>
/// A null property means the value is absent, and for the two preferences that is resolved to
/// the documented default rather than to "leave unchanged" — this is a replacement of the
/// editable fields, so an omitted preference is the default one, converging on the same value
/// the column would have held had the client never mentioned it.
/// </para>
/// </remarks>
public sealed record UpdateProfileRequest
{
    /// <summary>The name shown beside the caller's activity. Required, and must not be blank.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The profile bio. Optional; a whitespace-only value means "no bio" and is stored
    /// as null rather than as blanks.</summary>
    public string? Description { get; init; }

    /// <summary>One of <see cref="Constant.UserPreference.Themes"/>. Null means the default.</summary>
    public string? PreferredTheme { get; init; }

    /// <summary>One of <see cref="Constant.UserPreference.Languages"/>. Null means the default.</summary>
    public string? PreferredLanguage { get; init; }
}
