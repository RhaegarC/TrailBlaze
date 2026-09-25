/**
 * The wire shapes, as the API serialises them.
 *
 * These are the server's names, not the screens'. `mappers.ts` is where they become what the
 * export renders, so a field renamed on one side is a compile error in one file rather than a
 * silently blank screen.
 */

/** `ActivityType.All`, PascalCase on the wire. */
export type WireActivityType = "Public" | "Shared" | "Private";

/** `MediaKind.All`. Already matches the screens. */
export type WireMediaKind = "Image" | "Video";

/** `UserRole.All`. */
export type WireRole = "User" | "Admin";

export type WireTheme = "Dark" | "Light";
export type WireLanguage = "en" | "zh";

/** `ActivityResponse`. */
export interface WireActivity {
  id: string;
  title: string;
  location: string;
  /** `DateOnly` — `yyyy-MM-dd`, no time and no zone. */
  activityDate: string;
  description: string | null;
  type: WireActivityType;
  /** Null when the activity has no cover. Public for a `Public` activity, a short-lived SAS otherwise. */
  coverImageUrl: string | null;
  mediaCount: number;
  creatorDisplayName: string | null;
  /** Absent for an anonymous caller, and present for every signed-in one (Decision #30). */
  createdByUserId?: string | null;
}

/** `ActivityPage`, the body of `GET /api/activity`. */
export interface WireActivityPage {
  items: WireActivity[];
  /** The page index the server actually applied, after clamping. */
  page: number;
  /** The size it actually applied, after clamping. The screens page by this, not by what they asked for. */
  pageSize: number;
  /** Everything the caller may read, not the length of `items`. */
  total: number;
}

/** `MediaResponse`. */
export interface WireMedia {
  id: string;
  kind: WireMediaKind;
  contentType: string;
  sizeBytes: number;
  originalFileName: string;
  createdOn: string;
  uploadedByUserId: string;
  uploaderDisplayName: string | null;
}

/** `MediaUrlResponse`, the body of `GET /api/media/{id}/url`. */
export interface WireMediaUrl {
  url: string;
  expiresOnUtc: string;
}

/** `CoverResponse`. */
export interface WireCover {
  coverImageUrl: string;
}

/** `UserProfileResponse`. */
export interface WireProfile {
  /** The Entra object id, which is also what `WireActivity.createdByUserId` carries. */
  id: string;
  email: string | null;
  displayName: string | null;
  role: WireRole | null;
  description: string | null;
  /** Unsigned and public: the `avatars` container is readable, unlike `media`. */
  avatarUrl: string | null;
  preferredTheme: WireTheme;
  preferredLanguage: WireLanguage;
}

/** `CreateActivityRequest` and `UpdateActivityRequest` are the same five optional fields. */
export interface WireActivityInput {
  title: string;
  location: string;
  activityDate: string;
  description: string;
  type: WireActivityType;
}

/** `UpdateProfileRequest`. `id`, `email` and `role` have no property here, so a body cannot set them. */
export interface WireProfileInput {
  displayName: string;
  description: string;
  preferredTheme: WireTheme;
  preferredLanguage: WireLanguage;
}
