namespace TrailBlaze.Model;

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

        // Activity validation. Rejections, never rewrites: this is text the caller typed, so
        // shortening it would be storing something they did not write.
        public const string TitleRequired = "Title is required.";

        public const string LocationRequired = "Location is required.";

        public const string ActivityDateRequired = "Activity date is required.";

        public static readonly string TitleTooLong =
            $"Title must be {ActivityField.TitleLength} characters or fewer.";

        public static readonly string LocationTooLong =
            $"Location must be {ActivityField.LocationLength} characters or fewer.";

        public static readonly string TypeNotAllowed =
            $"Type must be one of: {string.Join(", ", ActivityType.All)}.";

        // Media. An image or a video that is neither on the allowlist is refused by the shared
        // message for its kind, so only a type belonging to no kind has its own.
        public static readonly string MediaTypeNotAllowed =
            $"The file must be one of: {string.Join(", ", Upload.MediaContentTypes)}.";

        public static readonly string MediaLimitReached =
            $"An activity can hold at most {MediaLimit.PerActivity} media items.";
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
    /// The claim names this app reads off a validated Entra ID token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Collected here so every name is read in one place rather than spelled out at the point
    /// of use. A claim name is a string the tenant defines, not this app — a typo in one is not
    /// a compile error but a silently null claim, which is a sign-in that succeeds with no
    /// identity attached. Naming them together is what makes that set reviewable.
    /// </para>
    /// <para>
    /// The pairs are deliberate. Entra ID issues short names (<c>oid</c>, <c>email</c>) on
    /// v2.0 tokens and WS-Federation URIs on v1.0 ones, and which arrives depends on the
    /// tenant's configuration rather than on this app, so both are read for a value that is
    /// the same either way.
    /// </para>
    /// </remarks>
    public static class Claim
    {
        /// <summary>Entra ID's short claim for the object id — the <c>users</c> key.</summary>
        public const string ObjectId = "oid";

        /// <summary>The long-form claim older tokens carry for the same value.</summary>
        public const string ObjectIdSchema =
            "http://schemas.microsoft.com/identity/claims/objectidentifier";

        /// <summary>Entra ID's short claim for the email address.</summary>
        public const string Email = "email";

        /// <summary>The WS-Federation claim older tokens carry for the same value.</summary>
        public const string EmailSchema =
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

        /// <summary>The user's display name, as the directory holds it.</summary>
        public const string Name = "name";

        /// <summary>The sign-in address. Not an email column's source — see
        /// <c>UserContextService.Email</c> — but a usable display name.</summary>
        public const string PreferredUsername = "preferred_username";
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

    /// <summary>The values <c>User.Role</c> accepts. Changing this list needs a migration, and
    /// <c>CK_Users_Role</c> is composed from it.</summary>
    public static class UserRole
    {
        /// <summary>An ordinary signed-in person, and the default.</summary>
        public const string User = "User";

        /// <summary>The administrator, granted by setting the row's <c>Role</c> column by hand.</summary>
        public const string Admin = "Admin";

        /// <summary>The values <c>User.Role</c> accepts.</summary>
        public static readonly string[] All = [User, Admin];
    }

    /// <summary>
    /// The values <c>Activity.Type</c> accepts. A closed set the engine also enforces:
    /// <c>CK_Activities_Type</c> is composed from it.
    /// </summary>
    public static class ActivityType
    {
        /// <summary>Anonymous callers may read it.</summary>
        public const string Public = "Public";

        /// <summary>Signed-in callers may read it.</summary>
        public const string Shared = "Shared";

        /// <summary>Its creator and administrators may read it.</summary>
        public const string Private = "Private";

        /// <summary>What an absent type means on create.</summary>
        public const string Default = Public;

        /// <summary>The values <c>Activity.Type</c> accepts.</summary>
        public static readonly string[] All = [Public, Shared, Private];
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
    /// The length each <c>activities</c> column is bounded to, from the PRD's data-model row.
    /// <c>Description</c> is absent because it is deliberately unbounded — it is the long-text
    /// field.
    /// </summary>
    public static class ActivityField
    {
        public const int TitleLength = 200;

        public const int LocationLength = 200;

        public const int TypeLength = 16;

        public const int CoverImageBlobPathLength = 512;
    }

    /// <summary>
    /// The page sizes the activity list accepts.
    /// </summary>
    /// <remarks>
    /// The cap is not a preference to be argued with at the call site: it is what keeps the
    /// endpoint from ever returning an unbounded set, so a caller asking for more is clamped
    /// rather than refused.
    /// </remarks>
    public static class ActivityPaging
    {
        /// <summary>Applied when a caller sends nothing, and to a size this endpoint will not honour.</summary>
        public const int DefaultPageSize = 10;

        /// <summary>The largest page this endpoint returns, whatever a caller asks for.</summary>
        public const int MaxPageSize = 100;
    }

    /// <summary>
    /// The values <c>Media.Kind</c> accepts. Derived from the content type rather than sent, so
    /// a caller cannot label a video an image — and the engine enforces the set with
    /// <c>CK_Media_Kind</c>, as it does for an activity's type.
    /// </summary>
    public static class MediaKind
    {
        public const string Image = "Image";

        public const string Video = "Video";

        /// <summary>The values <c>Media.Kind</c> accepts.</summary>
        public static readonly string[] All = [Image, Video];
    }

    /// <summary>
    /// The length each <c>media</c> column is bounded to, from the PRD's data-model row.
    /// </summary>
    public static class MediaField
    {
        public const int KindLength = 16;

        public const int BlobPathLength = 512;

        public const int ContentTypeLength = 128;

        public const int OriginalFileNameLength = 260;

        /// <summary>Holds an <c>activities.Id</c> — an app-assigned GUID. The activity's own key
        /// column is longer, because EF's key convention bound that one; comparing the two is a
        /// plain string comparison, so the widths need not match.</summary>
        public const int ReferenceIdLength = 128;
    }

    /// <summary>
    /// How many items one activity may carry (Decision #24).
    /// </summary>
    public static class MediaLimit
    {
        /// <summary>Counted across every uploader, because media is collaborative: the cap is the
        /// activity's, not the contributor's (Decision #27).</summary>
        public const int PerActivity = 20;
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

        /// <summary>Everything an activity's media may be, for the message a caller sees when the
        /// type they sent belongs to neither kind.</summary>
        public static readonly string[] MediaContentTypes =
            [.. ImageContentTypes, .. VideoContentTypes];

        /// <summary>The image cap: 10 MB. A file exactly at the cap is accepted.</summary>
        public const long ImageSizeCapBytes = 10L * 1024 * 1024;

        /// <summary>The video cap: 200 MB (Decision #24).</summary>
        public const long VideoSizeCapBytes = 200L * 1024 * 1024;

        /// <summary>
        /// The largest request body the media upload route accepts: the video cap plus room for the
        /// multipart envelope around it.
        /// </summary>
        /// <remarks>
        /// A route limit rather than a validation rule. Kestrel refuses a body over 30 MB and the
        /// form parser refuses a multipart body over 128 MB, both **before** the service sees
        /// anything — so without this the documented 400 for an oversize file would arrive as a bare
        /// 413 for every video over 128 MB, which is most of the ones the cap admits. The envelope
        /// allowance is what keeps a file exactly at the cap from being refused by its own boundary
        /// and headers.
        /// </remarks>
        public const long MaxMediaRequestBytes = VideoSizeCapBytes + MultipartEnvelopeBytes;

        /// <summary>Room for boundaries and part headers around a file at the cap.</summary>
        private const long MultipartEnvelopeBytes = 64L * 1024;
    }
}
