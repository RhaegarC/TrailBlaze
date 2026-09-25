/**
 * The wire shape into the shape the screens already render.
 *
 * The export takes its data as props and was written against fixtures, so its components have a
 * stated shape for an activity and for a media item. Translating here rather than in `App.tsx`
 * is what keeps the seam to the places that fetch, and leaves every screen below them untouched.
 */

import type { WireActivity, WireActivityType, WireMedia, WireProfile } from "./types";

/** What `ActivityCard`, `ActivityDetail` and `ActivityForm` expect. */
export interface ActivityView {
  id: string;
  title: string;
  location: string;
  /** `yyyy-MM-dd`, the only form `formatDate` parses. */
  activityDate: string;
  description: string;
  coverImageUrl: string | null;
  createdBy: string;
  /** Empty for an anonymous caller, who is told the creator's name but not their id. */
  createdByUserId: string;
  mediaCount: number;
  type: "public" | "shared" | "private";
}

/** What the media grid and the upload list expect. */
export interface MediaView {
  id: string;
  kind: "Image" | "Video";
  originalFileName: string;
  sizeBytes: number;
  /** A signed, expiring URL, empty until `getMediaUrl` has answered for this item. */
  url: string;
  contentType: string;
  uploadedBy: string;
  uploadedByUserId: string;
}

export function toActivityView(wire: WireActivity): ActivityView {
  return {
    id: wire.id,
    title: wire.title,
    location: wire.location,
    activityDate: wire.activityDate,
    description: wire.description ?? "",
    coverImageUrl: wire.coverImageUrl ?? null,
    createdBy: wire.creatorDisplayName ?? "",
    createdByUserId: wire.createdByUserId ?? "",
    mediaCount: wire.mediaCount,
    type: toActivityType(wire.type),
  };
}

export function toActivityType(type: WireActivityType): "public" | "shared" | "private" {
  return type.toLowerCase() as "public" | "shared" | "private";
}

/** The inverse, for a request body. An allowlisted set, so an unknown value cannot be sent. */
export function toWireActivityType(type: string): WireActivityType {
  switch (type) {
    case "shared":
      return "Shared";
    case "private":
      return "Private";
    default:
      return "Public";
  }
}

/**
 * A media item, with its `url` supplied by the caller.
 *
 * The URL is a separate request per item — a signed, short-lived bearer token — so it is not
 * something this mapping can fetch, and a screen that has not fetched it yet renders the item
 * without one rather than not at all.
 */
export function toMediaView(wire: WireMedia, url: string): MediaView {
  return {
    id: wire.id,
    kind: wire.kind,
    originalFileName: wire.originalFileName,
    sizeBytes: wire.sizeBytes,
    url,
    contentType: wire.contentType,
    uploadedBy: wire.uploaderDisplayName ?? "",
    uploadedByUserId: wire.uploadedByUserId,
  };
}

/** The profile, in the shape `UserProfile` renders. */
export interface ProfileView {
  id: string;
  email: string;
  displayName: string;
  role: "user" | "admin";
  description: string;
  avatarUrl: string | null;
  theme: "dark" | "light";
  lang: "en" | "zh";
}

export function toProfileView(wire: WireProfile): ProfileView {
  return {
    id: wire.id,
    email: wire.email ?? "",
    displayName: wire.displayName ?? "",
    role: wire.role === "Admin" ? "admin" : "user",
    description: wire.description ?? "",
    avatarUrl: wire.avatarUrl ?? null,
    theme: wire.preferredTheme === "Light" ? "light" : "dark",
    lang: wire.preferredLanguage === "zh" ? "zh" : "en",
  };
}
