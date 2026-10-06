/**
 * The screens' data, as hooks.
 *
 * Each hook owns one request and reports three things: what came back, whether it is still
 * coming, and the refusal if there was one. A refusal is kept as an `ApiError` rather than as
 * text, because 401, 403 and 404 are three different screens and flattening them here is what
 * would make them indistinguishable later.
 */

import { useCallback, useEffect, useState } from "react";

import { toApiError, type ApiError } from "../api/client";
import {
  getActivity,
  getMediaUrl,
  getProfile,
  listActivities,
  listMedia,
} from "../api/endpoints";
import {
  toActivityView,
  toMediaView,
  toProfileView,
  type ActivityView,
  type MediaView,
  type ProfileView,
} from "../api/mappers";
import { useAuth } from "../auth/store";
import type { WireMedia } from "../api/types";
import { hasLife, remember, urlFor } from "./mediaUrlCache";

interface Loadable {
  loading: boolean;
  error: ApiError | null;
  reload: () => void;
}

/** A counter that a `reload()` bumps, which re-runs the effect that depends on it. */
function useReload(): [number, () => void] {
  const [nonce, setNonce] = useState(0);
  return [nonce, useCallback(() => setNonce((n) => n + 1), [])];
}

/** Runs `load` on mount and whenever its dependencies or `nonce` change. */
function useLoad<T>(
  load: (signal: AbortSignal) => Promise<T>,
  apply: (value: T) => void,
  dependencies: unknown[],
  onFailure: (error: ApiError) => void
): [boolean, () => void] {
  const [loading, setLoading] = useState(true);
  const [nonce, reload] = useReload();

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    load(controller.signal)
      .then((value) => {
        if (!controller.signal.aborted) apply(value);
      })
      .catch((failure) => {
        if (!controller.signal.aborted) onFailure(toApiError(failure));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
    // The caller's dependency list is the effect's; `load` and `apply` close over the same
    // values, so re-creating them per render must not re-run the request.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...dependencies, nonce]);

  return [loading, reload];
}

// ─── The public list ───────────────────────────────────────────────────────────

export interface ActivitiesPage {
  items: ActivityView[];
  /** Everything the caller may read, which is not the length of `items`. */
  total: number;
  /** The size the server applied after clamping — what the pager steps by. */
  pageSize: number;
  page: number;
}

export function useActivities(page: number, pageSize: number): ActivitiesPage & Loadable {
  const [data, setData] = useState<ActivitiesPage>({
    items: [],
    total: 0,
    pageSize,
    page: 0,
  });
  const [error, setError] = useState<ApiError | null>(null);

  // Who the caller is, is part of what the list is: signing in admits `Shared` entries and the
  // caller's own `Private` ones. Adopting an account is asynchronous, so without this the first
  // fetch of every signed-in session is the anonymous one and nothing ever corrects it.
  const { status } = useAuth();

  const [loading, reload] = useLoad(
    (signal) => listActivities(page, pageSize, signal),
    (result) =>
      setData({
        items: result.items.map(toActivityView),
        total: result.total,
        pageSize: result.pageSize,
        page: result.page,
      }),
    [page, pageSize, status],
    setError
  );

  return { ...data, loading, error, reload };
}

/**
 * How many activities the caller may read, for the footer.
 *
 * A page of one row, because the total is the only thing wanted from it. Cheap and, more to the
 * point, honest: the footer said a fixture's length before, and a number nothing keeps in step
 * with the server is worse than a request.
 */
export function useActivityCount(): number {
  return useActivities(0, 1).total;
}

// ─── One activity ──────────────────────────────────────────────────────────────

export function useActivity(
  id: string
): { activity: ActivityView | null } & Omit<Loadable, "reload"> {
  const [activity, setActivity] = useState<ActivityView | null>(null);
  const [error, setError] = useState<ApiError | null>(null);

  // Same reason as the list: a `Private` entry answers the owner and the visitor differently, so
  // the caller belongs in the dependencies rather than in the fetch alone.
  const { status } = useAuth();

  const [loading] = useLoad(
    (signal) => getActivity(id, signal),
    (wire) => setActivity(toActivityView(wire)),
    [id, status],
    setError
  );

  return { activity, loading, error };
}

// ─── An activity's media ───────────────────────────────────────────────────────

/**
 * The media of one activity, each item carrying a read URL the listing supplied.
 *
 * No `enabled` flag: both routes authorize on the activity's own read rule, so a screen that may
 * render the activity is a screen that may fetch its media. A caller who may not read it is
 * refused 404, which is the same refusal the activity itself gets.
 */
export function useActivityMedia(activityId: string): { media: MediaView[] } & Loadable {
  const [media, setMedia] = useState<MediaView[]>([]);
  const [error, setError] = useState<ApiError | null>(null);

  // And again here: a `Shared` activity's media is refused to a visitor and served to a caller who
  // may read it, so the answer this hook holds is only valid for the caller who asked.
  const { status } = useAuth();

  const [loading, reload] = useLoad(
    async (signal) => {
      const items = await listMedia(activityId, signal);

      // The listing carries a URL and its expiry for every item, so a page of images costs one
      // request rather than one plus the number of images.
      const settled = await Promise.all(items.map((item) => withFreshUrl(item, signal)));

      return settled.map(toMediaView);
    },
    setMedia,
    [activityId, status],
    setError
  );

  return { media, loading, error, reload };
}

/**
 * The item with a URL worth rendering, minting one only when there is nothing left to render.
 *
 * The usual answer is the URL the listing already carried, so the per-item request this replaces is
 * normally not made at all. It is made for an item whose URL has run out, which happens to a page
 * left open past the signing window — and a mint inside the same boundary minute returns the string
 * it replaced, which is why the cache's margin is a whole minute rather than a few seconds.
 */
async function withFreshUrl(item: WireMedia, signal: AbortSignal): Promise<WireMedia> {
  const alreadyHeld = urlFor(item.id);
  if (alreadyHeld !== null) return { ...item, url: alreadyHeld };

  if (hasLife(item.url, item.expiresOnUtc)) {
    remember([item]);
    return item;
  }

  try {
    const minted = await getMediaUrl(item.id, signal);
    remember([{ id: item.id, ...minted }]);
    return { ...item, url: minted.url };
  } catch {
    // A single failure leaves that one tile without a source rather than emptying the section,
    // which is the behaviour the fan-out this replaced had too.
    return { ...item, url: "" };
  }
}

// ─── The caller's profile ──────────────────────────────────────────────────────

export function useProfile(enabled: boolean): { profile: ProfileView | null } & Loadable {
  const [profile, setProfile] = useState<ProfileView | null>(null);
  const [error, setError] = useState<ApiError | null>(null);

  const [loading, reload] = useLoad(
    async (signal) => (enabled ? getProfile(signal) : null),
    (wire) => setProfile(wire ? toProfileView(wire) : null),
    [enabled],
    setError
  );

  return { profile, loading, error, reload };
}
