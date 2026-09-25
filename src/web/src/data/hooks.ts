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

  const [loading, reload] = useLoad(
    (signal) => listActivities(page, pageSize, signal),
    (result) =>
      setData({
        items: result.items.map(toActivityView),
        total: result.total,
        pageSize: result.pageSize,
        page: result.page,
      }),
    [page, pageSize],
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

  const [loading] = useLoad(
    (signal) => getActivity(id, signal),
    (wire) => setActivity(toActivityView(wire)),
    [id],
    setError
  );

  return { activity, loading, error };
}

// ─── An activity's media ───────────────────────────────────────────────────────

/**
 * The media of one activity, each item carrying a freshly minted read URL.
 *
 * `enabled` is what keeps this off the wire for a visitor: the route is signed-in only, so a
 * fetch from an anonymous screen would be a 401 rather than a list, and the signed URLs are the
 * whole reason the route exists.
 */
export function useActivityMedia(activityId: string, enabled: boolean): { media: MediaView[] } & Loadable {
  const [media, setMedia] = useState<MediaView[]>([]);
  const [error, setError] = useState<ApiError | null>(null);

  const [loading, reload] = useLoad(
    async (signal) => {
      if (!enabled) return [];
      const items = await listMedia(activityId, signal);
      // One URL per item, all in flight at once: the grid renders every item, and a signed URL
      // is cheap to mint. A single failure leaves that one tile without a source rather than
      // emptying the section.
      const urls = await Promise.all(
        items.map((item) =>
          getMediaUrl(item.id, signal)
            .then((result) => result.url)
            .catch(() => "")
        )
      );
      return items.map((item, index) => toMediaView(item, urls[index]));
    },
    setMedia,
    [activityId, enabled],
    setError
  );

  return { media, loading, error, reload };
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
