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
    }
}
