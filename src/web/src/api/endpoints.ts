/**
 * One function per route the app calls. Nothing here decides what to render or when to call —
 * a hook does that, and a screen does the rendering.
 */

import { request } from "./client";
import type {
  WireActivity,
  WireActivityInput,
  WireActivityPage,
  WireCover,
  WireMedia,
  WireMediaUrl,
  WireProfile,
  WireProfileInput,
} from "./types";

// ─── Activities ────────────────────────────────────────────────────────────────

/**
 * Readable without a token, and wider with one: the visibility filter admits `Public` to everyone,
 * and a caller's own `Shared` and `Private` entries to that caller. Sending no token here is what
 * would make those two invisible to the person who owns them.
 */
export function listActivities(
  page: number,
  pageSize: number,
  signal?: AbortSignal
): Promise<WireActivityPage> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return request<WireActivityPage>(`/api/activity?${query}`, { signal });
}

/** Reads the caller's own entries too, for the reason above. A 404 is what an unreadable one answers. */
export function getActivity(id: string, signal?: AbortSignal): Promise<WireActivity> {
  return request<WireActivity>(`/api/activity/${encodeURIComponent(id)}`, { signal });
}

export function createActivity(body: WireActivityInput): Promise<WireActivity> {
  return request<WireActivity>("/api/activity", { method: "POST", body });
}

export function updateActivity(id: string, body: WireActivityInput): Promise<WireActivity> {
  return request<WireActivity>(`/api/activity/${encodeURIComponent(id)}`, {
    method: "PUT",
    body,
  });
}

export function deleteActivity(id: string): Promise<void> {
  return request<void>(`/api/activity/${encodeURIComponent(id)}`, { method: "DELETE" });
}

/** The cover is stored after the activity exists, so a create is two requests. */
export function uploadCover(id: string, file: File): Promise<WireCover> {
  const form = new FormData();
  form.append("file", file);
  return request<WireCover>(`/api/activity/${encodeURIComponent(id)}/cover`, {
    method: "POST",
    form,
  });
}

// ─── Media ─────────────────────────────────────────────────────────────────────

/** Signed-in only, unlike the activity it belongs to. Ordered oldest first. */
export function listMedia(activityId: string, signal?: AbortSignal): Promise<WireMedia[]> {
  return request<WireMedia[]>(`/api/activity/${encodeURIComponent(activityId)}/media`, {
    signal,
  });
}

/** Open to any signed-in caller who can read the activity (Decision #27). */
export function uploadMedia(activityId: string, file: File): Promise<WireMedia> {
  const form = new FormData();
  form.append("file", file);
  return request<WireMedia>(`/api/activity/${encodeURIComponent(activityId)}/media`, {
    method: "POST",
    form,
  });
}

/**
 * Uploader or administrator. Owning the activity is not enough.
 *
 * Uncalled: the export renders no control on a media item, and adding one would be authoring a
 * screen rather than wiring it. Feature 10 records that as a gap in the export.
 */
export function deleteMedia(mediaId: string): Promise<void> {
  return request<void>(`/api/media/${encodeURIComponent(mediaId)}`, { method: "DELETE" });
}

/**
 * Mints a read URL for one item. A 401 and a 404 are the only refusals: being able to read the
 * item is being able to fetch it, so there is no 403 to distinguish.
 */
export function getMediaUrl(mediaId: string, signal?: AbortSignal): Promise<WireMediaUrl> {
  return request<WireMediaUrl>(`/api/media/${encodeURIComponent(mediaId)}/url`, { signal });
}

// ─── Profile ───────────────────────────────────────────────────────────────────

/** Provisioned on this call, so it is the first request a new account should make. */
export function getProfile(signal?: AbortSignal): Promise<WireProfile> {
  return request<WireProfile>("/user/me", { signal });
}

export function updateProfile(body: WireProfileInput): Promise<WireProfile> {
  return request<WireProfile>("/user/me", { method: "PUT", body });
}

export function uploadAvatar(file: File): Promise<WireProfile> {
  const form = new FormData();
  form.append("file", file);
  return request<WireProfile>("/user/me/avatar", { method: "POST", form });
}

/** Succeeds even when there was no avatar: the caller asked for it not to be there. */
export function removeAvatar(): Promise<WireProfile> {
  return request<WireProfile>("/user/me/avatar", { method: "DELETE" });
}
