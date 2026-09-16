namespace TrailBlaze.Model.Profile;

/// <summary>
/// The caller's profile as the profile screen reads it: every field that route serves, in one
/// response rather than several.
/// </summary>
/// <remarks>
/// <para>
/// The entity is not returned directly, even though that would be less code. A response type
/// is what makes <c>AvatarBlobPath</c> an internal detail rather than part of the contract:
/// this exposes <see cref="AvatarUrl"/>, the URL the browser can actually fetch, and never the
/// storage path, which a client has no use for and which would freeze the container layout
/// into the public API.
/// </para>
/// <para>
/// Serialised with the host's default JSON options, so property names reach the client in
/// camelCase (<c>displayName</c>, <c>avatarUrl</c>). Feature 10's client binds those names, so
/// they are part of the contract rather than an accident of the serialiser.
/// </para>
/// </remarks>
public sealed record UserProfileResponse
{
    /// <summary>The caller's id — the Entra object id, since that is this row's key.</summary>
    public required string Id { get; init; }

    /// <summary>From the token. Null when the token carried no email claim.</summary>
    public string? Email { get; init; }

    public string? DisplayName { get; init; }

    /// <summary>The caller's own role, for display. Read-only here: nothing on the profile
    /// routes can change it.</summary>
    public string? Role { get; init; }

    public string? Description { get; init; }

    /// <summary>The resolved public URL of the avatar, or null when the caller has none.
    /// Unsigned, because the <c>avatars</c> container is public — see
    /// <c>IStorageRepository.CreatePublicUrl</c>.</summary>
    public string? AvatarUrl { get; init; }

    public required string PreferredTheme { get; init; }

    public required string PreferredLanguage { get; init; }
}
