using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Repository;
using TrailBlaze.Interface.Service;
using TrailBlaze.Model;
using TrailBlaze.Model.DatabaseEntity;
using TrailBlaze.Model.Profile;

namespace TrailBlaze.Service
{
    public sealed class UserService(
        IUserRepository userRepository,
        IStorageRepository storageRepository,
        IUserContextService userContext,
        UploadValidationService uploadValidation) : IUserService
    {
        /// <summary>
        /// The key a rejected avatar upload reports its error under. It matches the form field
        /// the route reads, so a client can put the message beside the control that caused it.
        /// </summary>
        private const string AvatarFormField = "file";

        /// <inheritdoc/>
        public async Task<User?> GetOrCreateAsync()
        {
            string? entraObjectId = userContext.EntraObjectId;

            // A validated token still might not carry an object id. There is no identity to
            // key a row on, so there is nothing to look up and — more importantly — nothing
            // that may be inserted: an unidentifiable caller must not create a user.
            if (string.IsNullOrWhiteSpace(entraObjectId))
            {
                return null;
            }

            User? user = await userRepository.GetAsync<User>(existing => existing.Id == entraObjectId);

            if (user is not null)
            {
                return user;
            }

            // The object id is the row's identity, so it is assigned here rather than left to
            // the constructor's generated key. The claims are shortened to fit their columns on
            // the way in -- see ToColumn.
            user = new User
            {
                Id = entraObjectId,
                DisplayName = ToColumn(userContext.ActorName, Constant.UserProfile.DisplayNameLength),
                Email = ToColumn(userContext.Email, Constant.UserProfile.EmailLength),
            };

            // The read above and this write cannot be one atomic step: the API runs several
            // replicas, and two of them can serve the same person's first sign-in at once, both
            // find nothing, and both insert. The primary key decides which one wins, and the
            // loser is told so rather than failed.
            bool inserted = await userRepository.AddIfAbsentAsync(user);

            if (inserted)
            {
                return user;
            }

            // This call lost the race, so `user` was never stored and the row that exists is the
            // other writer's. It is also the authoritative one -- the winner may have run with a
            // token carrying a different display name -- so the caller is handed what is stored
            // rather than what this call meant to store.
            return await userRepository.GetAsync<User>(existing => existing.Id == entraObjectId);
        }

        /// <inheritdoc/>
        public async Task<UserProfileResponse?> GetProfileAsync()
        {
            User? user = await GetOrCreateAsync();

            return user is null ? null : ToResponse(user);
        }

        /// <inheritdoc/>
        public async Task<ProfileOutcome> UpdateProfileAsync(UpdateProfileRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            User? user = await GetOrCreateAsync();

            if (user is null)
            {
                return ProfileOutcome.NoCaller();
            }

            var errors = new Dictionary<string, string[]>();

            // Everything is validated before anything is written, so a request with two bad fields
            // reports both rather than making the client discover them one round trip at a time.
            string? displayName = request.DisplayName?.Trim();

            if (string.IsNullOrEmpty(displayName))
            {
                errors[nameof(UpdateProfileRequest.DisplayName)] = [Constant.Message.DisplayNameRequired];
            }
            else if (displayName.Length > Constant.UserProfile.DisplayNameLength)
            {
                // Rejected, not shortened. This text was typed by the caller, so truncating it
                // would be silently discarding what they wrote -- the opposite of the rule for a
                // token claim, which they did not write and so cannot be asked to fix.
                errors[nameof(UpdateProfileRequest.DisplayName)] = [Constant.Message.DisplayNameTooLong];
            }

            string? description = Normalise(request.Description);

            if (description is not null && description.Length > Constant.UserProfile.DescriptionLength)
            {
                errors[nameof(UpdateProfileRequest.Description)] = [Constant.Message.DescriptionTooLong];
            }

            // An absent preference means the default rather than "leave unchanged": this replaces
            // the editable fields, so omitting one converges on the same value the column would
            // have held had the client never mentioned it.
            string theme = request.PreferredTheme ?? Constant.UserPreference.DarkTheme;

            // Exact match, deliberately: these values are compared by readers and stored verbatim,
            // so accepting "dark" would put a value in the column that nothing recognizes.
            if (!Constant.UserPreference.Themes.Contains(theme))
            {
                errors[nameof(UpdateProfileRequest.PreferredTheme)] = [Constant.Message.ThemeNotAllowed];
            }

            string language = request.PreferredLanguage ?? Constant.UserPreference.English;

            if (!Constant.UserPreference.Languages.Contains(language))
            {
                errors[nameof(UpdateProfileRequest.PreferredLanguage)] = [Constant.Message.LanguageNotAllowed];
            }

            if (errors.Count > 0)
            {
                return ProfileOutcome.Rejected(errors);
            }

            // Only the four editable fields are assigned. Role, Email and Id are not reachable
            // from here at all -- not because they are checked, but because nothing above this
            // line ever read them out of the request.
            user.DisplayName = displayName;
            user.Description = description;
            user.PreferredTheme = theme;
            user.PreferredLanguage = language;

            await userRepository.UpdateAsync(user);

            return ProfileOutcome.Completed(ToResponse(user));
        }

        /// <inheritdoc/>
        public async Task<ProfileOutcome> SetAvatarAsync(Stream content, string? contentType, long sizeBytes)
        {
            ArgumentNullException.ThrowIfNull(content);

            string? rejection = uploadValidation.ValidateImage(contentType, sizeBytes);

            if (rejection is not null)
            {
                // Rejected before the caller is even resolved, so a bad upload writes nothing: no
                // blob to orphan, and no row touched.
                return ProfileOutcome.Rejected(AvatarFormField, rejection);
            }

            User? user = await GetOrCreateAsync();

            if (user is null)
            {
                return ProfileOutcome.NoCaller();
            }

            string path = AvatarPathFor(user.Id, contentType!);
            string? previousPath = user.AvatarBlobPath;

            await storageRepository.UploadAsync(
                Constant.StorageContainer.Avatars, path, content, contentType!);

            user.AvatarBlobPath = path;
            await userRepository.UpdateAsync(user);

            // Deleting the old blob comes after the row points at the new one, never before. The
            // reverse order can leave the row naming a blob that is already gone -- a broken avatar
            // nobody can diagnose. This order can at worst leave one unreferenced blob, which is
            // inert and collectable; and while both exist, nothing a user can see is missing.
            if (!string.IsNullOrWhiteSpace(previousPath))
            {
                await storageRepository.DeleteAsync(Constant.StorageContainer.Avatars, previousPath);
            }

            return ProfileOutcome.Completed(ToResponse(user));
        }

        /// <inheritdoc/>
        public async Task<ProfileOutcome> RemoveAvatarAsync()
        {
            User? user = await GetOrCreateAsync();

            if (user is null)
            {
                return ProfileOutcome.NoCaller();
            }

            string? path = user.AvatarBlobPath;

            // Having no avatar is a normal state, not a failure -- most users have none. So this
            // returns success with zero storage calls rather than asking the vendor to delete
            // something that was never there, which is what makes "remove what you do not have" a
            // no-op instead of an error the caller has to interpret.
            if (string.IsNullOrWhiteSpace(path))
            {
                return ProfileOutcome.Completed(ToResponse(user));
            }

            user.AvatarBlobPath = null;
            await userRepository.UpdateAsync(user);

            // Row first, then blob, for the same reason as on replacement: this order never leaves
            // the row naming a blob that is missing.
            await storageRepository.DeleteAsync(Constant.StorageContainer.Avatars, path);

            return ProfileOutcome.Completed(ToResponse(user));
        }

        /// <summary>
        /// The row as the client sees it. The stored path is resolved to a public URL here and
        /// never handed out, so the container layout stays an internal detail.
        /// </summary>
        private UserProfileResponse ToResponse(User user) => new()
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role,
            Description = user.Description,
            AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarBlobPath)
                ? null
                : storageRepository
                    .CreatePublicUrl(Constant.StorageContainer.Avatars, user.AvatarBlobPath)
                    .ToString(),
            PreferredTheme = user.PreferredTheme,
            PreferredLanguage = user.PreferredLanguage,
        };

        /// <summary>
        /// Where a newly uploaded avatar is stored.
        /// </summary>
        /// <remarks>
        /// The folder is the caller's id, which is an Entra object id — a GUID, so it is safe as a
        /// path segment with nothing to escape. A display name would not be, and is not used.
        /// <para>
        /// A fresh name on every upload rather than a fixed one is what makes replacement safe: a
        /// stable path would let a browser keep serving the previous image from cache after the
        /// row had already moved on, with nothing to invalidate it.
        /// </para>
        /// </remarks>
        private static string AvatarPathFor(string userId, string contentType) =>
            $"{userId}/{Guid.NewGuid():N}{UploadValidationService.FileExtensionFor(contentType)}";

        /// <summary>
        /// A bio as it is stored: trimmed, with whitespace-only collapsed to null.
        /// </summary>
        /// <remarks>
        /// The same rule an activity's <c>Description</c> follows, so the two do not diverge. Null
        /// and a run of spaces mean the same thing to a reader — no bio — and storing both would
        /// make every reader decide which one it was holding.
        /// </remarks>
        private static string? Normalise(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>
        /// A token claim, shortened to the column it is going into. Whitespace-only becomes null.
        /// </summary>
        /// <remarks>
        /// Provisioning cannot reject this value the way a profile update rejects what a user
        /// typed: the caller did not type it. A display name longer than the column is Entra's
        /// answer about a real person, and refusing to sign them in over it would be this app
        /// deciding their name is invalid. Shortening it keeps the row insertable instead, which
        /// is the failure that costs the least.
        /// </remarks>
        /// <param name="claim">The value from the validated token, possibly null.</param>
        /// <param name="maxLength">The column's bound, from <see cref="Constant.UserProfile"/>.</param>
        private static string? ToColumn(string? claim, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(claim))
            {
                return null;
            }

            string trimmed = claim.Trim();

            if (trimmed.Length <= maxLength)
            {
                return trimmed;
            }

            // Cutting between the halves of a surrogate pair leaves an unpaired surrogate, which
            // is not a well-formed string and would fail the JSON serialiser when the profile is
            // read back -- a save that succeeds and a read that throws. Backing off one character
            // keeps the value well-formed at the cost of one code point.
            int cut = char.IsHighSurrogate(trimmed[maxLength - 1]) ? maxLength - 1 : maxLength;

            return trimmed[..cut];
        }
    }
}
