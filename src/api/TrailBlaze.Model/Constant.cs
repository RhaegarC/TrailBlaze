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

        // Methods rather than constants, because the cap they name is configuration: a fixed string
        // would report the default to an operator who had raised it, telling a caller their file
        // exceeded a size it was measured against nowhere.
        public static string ImageTooLarge(long capBytes) =>
            $"The image must be {capBytes / (1024 * 1024)} MB or smaller.";

        public static readonly string VideoTypeNotAllowed =
            $"The file must be one of: {string.Join(", ", Upload.VideoContentTypes)}.";

        public static string VideoTooLarge(long capBytes) =>
            $"The video must be {capBytes / (1024 * 1024)} MB or smaller.";

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
            $"You can add at most {MediaLimit.PerContributorPerActivity} media items to an activity.";
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

        /// <summary>How many minutes a media read URL is given. Optional, and held inside
        /// <see cref="SignedUrlLifetime.Maximum"/> whatever it says.</summary>
        public const string MediaUrlTtlMinutes = "MediaUrlTtlMinutes";

        /// <summary>The largest image an upload may carry, in bytes. Optional, and held inside
        /// <see cref="Upload.MaxImageSizeCapBytes"/> whatever it says.</summary>
        public const string ImageUploadCapBytes = "ImageUploadCapBytes";

        /// <summary>The largest video an upload may carry, in bytes. Optional, and held inside
        /// <see cref="Upload.MaxVideoSizeCapBytes"/> whatever it says.</summary>
        public const string VideoUploadCapBytes = "VideoUploadCapBytes";

        /// <summary>The quality an image's derivative is re-encoded at. Optional, and held inside
        /// <see cref="Thumbnail.QualityCeiling"/> whatever it says.</summary>
        public const string ThumbnailQuality = "ThumbnailQuality";

        /// <summary>The longest edge an image's derivative may have, in pixels. Optional, and held
        /// inside <see cref="Thumbnail.MaxDimensionCeiling"/> whatever it says.</summary>
        public const string ThumbnailMaxDimension = "ThumbnailMaxDimension";
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

    /// <summary>
    /// How long a browser may re-show an item's bytes without asking for them again.
    /// </summary>
    /// <remarks>
    /// Bounded well below <see cref="SignedUrlLifetime.Default"/> because the URL is the control and
    /// this is not: a cached copy is re-shown after its token has lapsed, which is what a window means
    /// rather than something the window fails to prevent. <c>private</c> keeps an intermediary from
    /// storing a copy at all, and it is never <c>immutable</c> — a cover's path changes when an entry
    /// crosses the public line, so nothing served this way is content-stable.
    /// </remarks>
    public static class MediaCache
    {
        /// <summary>The window, in seconds, after which a browser must ask again.</summary>
        /// <remarks>
        /// Also the window <see cref="SignedUrlLifetime.Boundary"/> rounds to, so changing this moves
        /// every signed URL's granularity with it — which is the intent, since a URL that changes
        /// sooner than the copy it names lapses is a URL that misses a cache it could have hit.
        /// </remarks>
        public const int MaxAgeSeconds = 300;

        /// <summary>The directive media bytes are stored with.</summary>
        public static readonly string Directive = $"private, max-age={MaxAgeSeconds}";
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
    /// How long a cover URL stays valid when the bytes are private.
    /// </summary>
    /// <remarks>
    /// A SAS is a bearer token, so the expiry is the real control rather than the URL's secrecy.
    /// Feature 07 mints the media equivalent on terms of its own — two numbers, not one. Both are
    /// held inside <see cref="SignedUrlLifetime.Maximum"/>.
    /// </remarks>
    public static class CoverUrl
    {
        /// <summary>The lifetime of a `Shared` or `Private` entry's cover link.</summary>
        public static readonly SignedUrlLifetime Lifetime = new(TimeSpan.FromMinutes(15));
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

        /// <summary>Mirrors <see cref="BlobPathLength"/>: the derivative's path is composed the same
        /// way, so a column narrower than the original's could not hold one.</summary>
        public const int ThumbnailPathLength = 512;

        public const int ContentTypeLength = 128;

        public const int OriginalFileNameLength = 260;

        /// <summary>Holds an <c>activities.Id</c> — an app-assigned GUID. The activity's own key
        /// column is longer, because EF's key convention bound that one; comparing the two is a
        /// plain string comparison, so the widths need not match.</summary>
        public const int ReferenceIdLength = 128;
    }

    /// <summary>
    /// The derivative an uploaded image is stored with (Decision #31): what it is encoded as, and
    /// the limits that decide whether an upload is decoded at all.
    /// </summary>
    /// <remarks>
    /// The decoder limits are the safety half and are not tunable, unlike the quality and the
    /// maximum dimension, which are settings precisely so they can be raised without a rebuild.
    /// <see cref="DecoderMaxDimension"/> and <see cref="DecoderMemoryBytes"/> bound the two
    /// allocations an upload controls: the pixels a file claims and the memory decoding them takes.
    /// Both are needed — the per-side limit is what the decoders honour, and a decoded image that
    /// stays under it can still be large enough to exhaust memory.
    /// </remarks>
    public static class Thumbnail
    {
        /// <summary>The quality a derivative is re-encoded at unless configuration says otherwise.</summary>
        /// <remarks>Very lossy on purpose, which is visible on the enlarged view, and a setting so it
        /// can be turned up.</remarks>
        public const int QualityDefault = 5;

        /// <summary>The highest quality configuration may ask for.</summary>
        public const int QualityCeiling = 100;

        /// <summary>The longest edge a derivative may have unless configuration says otherwise.</summary>
        /// <remarks>The setting that decides whether the enlarged view is legible, since the
        /// derivative is what every surface receives.</remarks>
        public const int MaxDimensionDefault = 1600;

        /// <summary>The highest maximum dimension configuration may ask for.</summary>
        public const int MaxDimensionCeiling = 4096;

        /// <summary>What a derivative is encoded as, and the extension it is named with.</summary>
        public const string ContentType = "image/jpeg";

        /// <inheritdoc cref="ContentType"/>
        public const string FileExtension = ".jpg";

        /// <summary>What a derivative's name carries between its stem and its extension.</summary>
        /// <remarks>Load-bearing: an uploaded JPEG's extension is already <see cref="FileExtension"/>,
        /// so a derivative named from the stem alone would be written onto the original's path.</remarks>
        public const string PathSuffix = "-thumb";

        /// <summary>The longest edge an uploaded image may have before it is refused undecoded.</summary>
        /// <remarks>Refuses a decompression bomb rather than shaping an ordinary photo, and is checked
        /// against the file's header so the refusal costs no allocation.</remarks>
        public const int DecoderMaxDimension = 20_000;

        /// <summary>The pixel cache a decode may use, in bytes.</summary>
        /// <remarks>A ceiling rather than a hint: with the disk cache off in <c>ThumbnailService</c>, a
        /// file needing more fails to decode instead of spilling to the container's volume.</remarks>
        public const long DecoderMemoryBytes = 268_435_456;

        /// <summary>The most frames a decode may hold.</summary>
        /// <remarks>A bound rather than a target of one: this limit refuses a decode, so a single-frame
        /// list would reject every animation instead of reading its first frame.</remarks>
        public const long DecoderMaxFrames = 128;
    }

    /// <summary>
    /// How many items one contributor may add to one activity (Decision #24).
    /// </summary>
    public static class MediaLimit
    {
        /// <summary>One contributor's items on one activity, not the activity's total: media is
        /// collaborative (Decision #27), so the cap bounds what one person adds rather than what
        /// everyone together adds.</summary>
        public const int PerContributorPerActivity = 50;
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
    /// 1024 * 1024 is the number a browser, a fetch client and an upload control all agree on;
    /// 10,000,000 would make "exactly at the cap" a different file for each of them.
    /// </para>
    /// <para>
    /// The defaults and the ceilings live here while the configured value does not, because a route
    /// attribute takes a compile-time constant and a setting is not one: <see cref="UploadSizeCaps"/>
    /// is where configuration is clamped to the ceiling declared below.
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

        /// <summary>The image cap applied when none is configured: 25 MB. A file exactly at the cap
        /// is accepted.</summary>
        public const long DefaultImageSizeCapBytes = 25L * 1024 * 1024;

        /// <summary>The video cap applied when none is configured: 200 MB (Decision #24).</summary>
        public const long DefaultVideoSizeCapBytes = 200L * 1024 * 1024;

        /// <summary>The largest image cap any setting may reach, and the value the image routes
        /// carry as their request-size limit.</summary>
        public const long MaxImageSizeCapBytes = 100L * 1024 * 1024;

        /// <summary>The largest video cap any setting may reach, and the value the media route
        /// carries as its request-size limit.</summary>
        public const long MaxVideoSizeCapBytes = 512L * 1024 * 1024;

        /// <summary>
        /// The largest request body the media upload route accepts: the video ceiling plus room for
        /// the multipart envelope around it.
        /// </summary>
        /// <remarks>
        /// A route limit rather than a validation rule, and derived from the ceiling rather than the
        /// default: Kestrel refuses a body over 30 MB and the form parser refuses a multipart body
        /// over 128 MB, both before the service sees anything, so a limit set to the default would
        /// answer an oversize video with a bare 413 rather than the documented 400. The envelope
        /// allowance is what keeps a file exactly at the ceiling from being refused by its own
        /// boundary and headers.
        /// </remarks>
        public const long MaxMediaRequestBytes = MaxVideoSizeCapBytes + MultipartEnvelopeBytes;

        /// <summary>
        /// The largest request body the cover and avatar routes accept: the image ceiling plus room
        /// for the multipart envelope around it.
        /// </summary>
        /// <remarks>
        /// Both routes carried no limit while the image cap was 10 MB, which sat below Kestrel's
        /// 30 MB default and the form parser's 128 MB one; the ceiling is now above both, so without
        /// this an acceptable cover would be answered with a bare 413.
        /// </remarks>
        public const long MaxImageRequestBytes = MaxImageSizeCapBytes + MultipartEnvelopeBytes;

        /// <summary>Room for boundaries and part headers around a file at the cap.</summary>
        private const long MultipartEnvelopeBytes = 64L * 1024;
    }
}
