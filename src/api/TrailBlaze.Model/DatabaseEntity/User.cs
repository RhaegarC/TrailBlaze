namespace TrailBlaze.Model.DatabaseEntity;

/// <summary>
/// A person the API knows about. The row exists because someone signed in, never because
/// someone registered: <see cref="EntityBase.Id"/> is the Entra object id, so the primary key
/// is the identity the token carries.
/// </summary>
/// <remarks>
/// That key shape is deliberate and was decided rather than inherited — see the PRD's
/// <c>users</c> data-model row. The consequence is that this id is a durable Entra
/// identifier, which is why anonymous payloads name a creator by display name and never by
/// id.
/// </remarks>
public sealed class User : EntityBase
{
    public string? DisplayName { get; set; }

    /// <summary>
    /// What this person may do, from the closed set in <see cref="Constant.UserRole"/>.
    /// Non-nullable with a default of <c>User</c>, and the two halves of that say different
    /// things: non-nullable because every reader of this column is an authorization decision
    /// and none of them may have to decide what an absent role means, and <c>User</c> because
    /// the value a row lands on when nothing said otherwise must be the one that grants
    /// nothing.
    /// </summary>
    /// <remarks>
    /// The value is a stored fact rather than a claim. Nothing in this app reads a role out of
    /// a token, which is why this column is the only source a check has —
    /// <c>CallerRoleService</c>. The column is also constrained to the set at the database, so
    /// a value outside it cannot be stored by anything, this application included.
    /// </remarks>
    public string Role { get; set; } = Constant.UserRole.User;

    /// <summary>The profile bio. Nullable, and normalised so that whitespace-only is null.</summary>
    public string? Description { get; set; }

    /// <summary>From the validated token's email claim. Nullable: not every token carries one.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// Path within the public <c>avatars</c> container, or null for a user who has never
    /// uploaded one. Nullable because having no avatar is a normal state, not a missing
    /// value — which is what lets removing one be a no-op rather than a special case.
    /// </summary>
    public string? AvatarBlobPath { get; set; }

    /// <summary>
    /// Presentation preference, not a permission. Non-nullable with a database default so no
    /// reader has to decide what an absent preference means.
    /// </summary>
    public string PreferredTheme { get; set; } = Constant.UserPreference.DarkTheme;

    /// <inheritdoc cref="PreferredTheme"/>
    public string PreferredLanguage { get; set; } = Constant.UserPreference.English;
}
