namespace TrailBlaze.Model
{
    public static class Constant
    {
        public static class App
        {
            public const string CORSPolicyName = "AllowSpecificOrigin";
            public const string HealthCheckUrl = "/health";
        }

        public static class Message
        {
            public const string NoAllowedOrigins = "Allowed origins not configured. Set the 'AllowedOrigins' configuration value (comma-separated).";

            public const string NoDbConnection = "Database connection not configured. Set the 'DbConnection' configuration value.";

            public const string NoBlobConnection = "Blob storage connection not configured. Set the 'BlobConnection' configuration value.";

            // Profile validation. Each of these is a rejection rather than a rewrite: this is
            // text a user typed, so shortening it silently would be losing their input, and the
            // alternative to rejecting it is storing something they did not write.
            public const string DisplayNameRequired = "Display name is required.";

            public const string NoFileUploaded = "No file was sent.";

            public const string DescriptionTooLong =
                "Description must be 500 characters or fewer.";

            // Composed from the allowlists rather than written out beside them, so a value added
            // to one cannot leave a message naming the old set. Correct prose is exactly the kind
            // of thing that goes stale unnoticed.
            public static readonly string DisplayNameTooLong =
                $"Display name must be {UserProfile.DisplayNameLength} characters or fewer.";

            public static readonly string ThemeNotAllowed =
                $"Preferred theme must be one of: {string.Join(", ", UserPreference.Themes)}.";

            public static readonly string LanguageNotAllowed =
                $"Preferred language must be one of: {string.Join(", ", UserPreference.Languages)}.";

            public static readonly string ImageTypeNotAllowed =
                $"The file must be one of: {string.Join(", ", Upload.ImageContentTypes)}.";

            public static readonly string ImageTooLarge =
                $"The image must be {Upload.ImageSizeCapBytes / (1024 * 1024)} MB or smaller.";

            public static readonly string VideoTypeNotAllowed =
                $"The file must be one of: {string.Join(", ", Upload.VideoContentTypes)}.";

            public static readonly string VideoTooLarge =
                $"The video must be {Upload.VideoSizeCapBytes / (1024 * 1024)} MB or smaller.";
        }

        public static class ConfigKey
        {
            public const string DBCon = "DbConnection";

            public const string TenantId = "TenantId";

            public const string Audience = "Audience";

            public const string AllowedOrigins = "AllowedOrigins";

            /// <summary>Azure Storage connection string. No container or account name is
            /// configured separately: the containers are the closed set in
            /// <see cref="StorageContainer"/>.</summary>
            public const string BlobConnection = "BlobConnection";
        }

        /// <summary>
        /// The containers the storage abstraction addresses. This set is closed: the container
        /// name is the whole of the public/private answer, so adding one is a deliberate act
        /// rather than something a call site decides by passing a string. Public means the blob
        /// is readable by URL alone; private means the only way in is a short-lived SAS minted
        /// by the storage service.
        /// </summary>
        public static class StorageContainer
        {
            /// <summary>Public. Cover images for activities.</summary>
            public const string Covers = "covers";

            /// <summary>Public. User avatars.</summary>
            public const string Avatars = "avatars";

            /// <summary>Private. Activity media, reached only through a short-lived SAS URL.</summary>
            public const string Media = "media";
        }

        /// <summary>
        /// The two presentation preferences a profile carries, and the values each accepts. A
        /// closed set rather than free text: both columns are non-nullable with a default, so a
        /// value outside this list is not a variation to tolerate but a caller to reject.
        /// </summary>
        /// <remarks>
        /// Neither carries authorization meaning. The role is its own column and the token is
        /// never consulted for privilege, so a caller who sets a theme has changed a preference
        /// and nothing else.
        /// </remarks>
        public static class UserPreference
        {
            public const string DarkTheme = "Dark";
            public const string LightTheme = "Light";
            public const string English = "en";
            public const string Chinese = "zh";

            /// <summary>The values <c>User.PreferredTheme</c> accepts.</summary>
            public static readonly string[] Themes = [DarkTheme, LightTheme];

            /// <summary>The values <c>User.PreferredLanguage</c> accepts.</summary>
            public static readonly string[] Languages = [English, Chinese];
        }

        /// <summary>
        /// The length each <c>users</c> column is bounded to, from the PRD's data-model row.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These are here rather than only in the fluent configuration because two layers need
        /// the same number for opposite reasons. The mapping bounds the column, so the database
        /// rejects anything longer; <c>UserService</c> shortens a token claim to the same length
        /// before inserting, because a claim the caller never typed cannot be rejected. A second
        /// copy of the number would let those drift, and the failure that produces is a save
        /// that throws on a value the service believed it had already made fit.
        /// </para>
        /// <para>
        /// The repository tier asserts each column's actual bound against a literal, so a change
        /// here that the mapping does not follow still fails a test.
        /// </para>
        /// </remarks>
        public static class UserProfile
        {
            public const int DisplayNameLength = 200;

            public const int EmailLength = 320;

            public const int DescriptionLength = 500;

            public const int AvatarBlobPathLength = 512;

            public const int RoleLength = 16;

            /// <summary>Shared by <c>PreferredTheme</c> and <c>PreferredLanguage</c>: both hold
            /// one of a handful of short tokens.</summary>
            public const int PreferenceLength = 16;
        }

        /// <summary>
        /// What an uploaded file may be: the content types each kind accepts and the size cap each
        /// is held to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Defined once for every upload route — an avatar, a cover and activity media — so the
        /// three cannot develop three different ideas of what an image is. Whether a blob ends up
        /// public or private is a separate question with a separate answer, and it lives in
        /// <see cref="StorageContainer"/>: this type is only about what the bytes are allowed to
        /// be.
        /// </para>
        /// <para>
        /// The caps are Decision #24's. Read as mebibytes rather than decimal megabytes, because
        /// 10 * 1024 * 1024 is the number a browser, a fetch client and an upload control all
        /// agree on; 10,000,000 would make "exactly at the cap" a different file for each of them.
        /// </para>
        /// </remarks>
        public static class Upload
        {
            /// <summary>The image types a cover or an avatar may be.</summary>
            public static readonly string[] ImageContentTypes =
                ["image/jpeg", "image/png", "image/webp", "image/gif"];

            /// <summary>The video types activity media may be.</summary>
            public static readonly string[] VideoContentTypes = ["video/mp4", "video/quicktime"];

            /// <summary>The image cap: 10 MB. A file exactly at the cap is accepted.</summary>
            public const long ImageSizeCapBytes = 10L * 1024 * 1024;

            /// <summary>The video cap: 200 MB (Decision #24). Unused until feature 06, kept here
            /// so the two caps are read together and neither is invented at a call site.</summary>
            public const long VideoSizeCapBytes = 200L * 1024 * 1024;
        }
    }
}
