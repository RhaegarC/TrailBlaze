namespace TrailBlaze.Model.DatabaseEntity
{
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

        public string? Role { get; set; }

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
}
