namespace TrailBlaze.Interface.Service;

using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Model.Profile;

/// <summary>
/// The caller's own record and profile. Every member acts on the request in flight rather
/// than on an argument, so there is no parameter through which one caller could name another.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// The caller's own row, inserted on first sight. Resolved from the request in flight
    /// rather than from an argument, so a caller cannot ask for somebody else's record.
    /// </summary>
    /// <returns>The caller's row, or null when the request carries no Entra Object ID to
    /// key one on.</returns>
    Task<User?> GetOrCreateAsync();

    /// <summary>
    /// The caller's profile as the profile screen reads it, in one response.
    /// </summary>
    /// <returns>The profile, or null when the request carries no Entra Object ID.</returns>
    Task<UserProfileResponse?> GetProfileAsync();

    /// <summary>
    /// Applies an edit to the caller's own row.
    /// </summary>
    /// <remarks>
    /// The request cannot reach <c>Id</c>, <c>Email</c> or <c>Role</c>, whatever it carries —
    /// see <see cref="UpdateProfileRequest"/>. Validation happens here rather than in
    /// attributes on the request type because "non-blank after trimming" and "whitespace-only
    /// means no bio" are rules about the domain, not about the shape of the JSON.
    /// </remarks>
    /// <param name="request">The fields to apply.</param>
    /// <returns>The updated profile, the reasons the input was rejected, or no caller.</returns>
    Task<ProfileOutcome> UpdateProfileAsync(UpdateProfileRequest request);

    /// <summary>
    /// Stores an avatar for the caller in the public <c>avatars</c> container and points their
    /// row at it, removing any previous one.
    /// </summary>
    /// <param name="content">The bytes to store.</param>
    /// <param name="contentType">The declared content type, validated against the shared image
    /// allowlist before anything is written.</param>
    /// <param name="sizeBytes">The size the cap is checked against.</param>
    /// <returns>The updated profile with the new public URL, the reason the upload was
    /// rejected, or no caller.</returns>
    Task<ProfileOutcome> SetAvatarAsync(Stream content, string? contentType, long sizeBytes);

    /// <summary>
    /// Clears the caller's avatar and deletes its blob.
    /// </summary>
    /// <returns>The updated profile, or no caller. A caller who has no avatar is a success
    /// rather than a 404 — they asked for it not to be there, and it is not.</returns>
    Task<ProfileOutcome> RemoveAvatarAsync();
}
