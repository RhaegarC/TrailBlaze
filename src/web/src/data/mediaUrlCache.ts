/**
 * The media read URLs the app is holding, and the instant each stops working.
 *
 * A media URL is a bearer token with a deadline, and the listing hands the deadline over with the
 * URL — which is what lets this decide rather than guess. A URL with life left is reused exactly as
 * it stands, so the browser's cache key stays still; one inside the safety margin is replaced before
 * it is rendered, because a tile whose source lapses mid-session is a broken image rather than a
 * slow one.
 *
 * The margin is a whole minute because the server rounds the minting instant down to a minute
 * boundary: a re-mint inside the same boundary returns the very string it was meant to replace, so
 * a margin shorter than the boundary could decide to refresh and be handed back what it already had.
 *
 * Nothing here performs a request. The cache answers questions about what is held; the hook that
 * asks them does the minting.
 */

/** How much life a URL must have left to be worth rendering. */
export const SafetyMarginSeconds = 60;

/** One item's URL and the instant it stops working, in the shape the wire carries them. */
export interface HeldMediaUrl {
  id: string;
  url: string;
  expiresOnUtc: string;
}

const held = new Map<string, HeldMediaUrl>();

/** Seconds of life left, or negative infinity for an instant that is not a date at all. */
function lifeLeft(expiresOnUtc: string, now: number): number {
  const expiry = Date.parse(expiresOnUtc);

  // An unreadable instant is treated as no life rather than as forever: the only safe answer to
  // "when does this stop working?" is the one that leads to a fresh URL.
  return Number.isNaN(expiry) ? Number.NEGATIVE_INFINITY : (expiry - now) / 1000;
}

/** Whether a URL is worth rendering at `now`: it exists and it outlives the safety margin. */
export function hasLife(url: string, expiresOnUtc: string, now = Date.now()): boolean {
  return url !== "" && lifeLeft(expiresOnUtc, now) > SafetyMarginSeconds;
}

/**
 * Takes the URLs a listing handed over, keeping the longest-lived one for each item.
 *
 * A later listing supersedes an earlier one, and the comparison is on the expiry rather than on
 * which arrived last — replacing a URL that has more life with one that has less would shorten what
 * the page can render for no gain.
 */
export function remember(candidates: readonly HeldMediaUrl[], now = Date.now()): void {
  for (const candidate of candidates) {
    if (!hasLife(candidate.url, candidate.expiresOnUtc, now)) continue;

    const current = held.get(candidate.id);
    if (current && lifeLeft(current.expiresOnUtc, now) >= lifeLeft(candidate.expiresOnUtc, now)) {
      continue;
    }

    held.set(candidate.id, candidate);
  }
}

/** What is held for `id`, or null when nothing is held or what is held has lapsed. */
export function urlFor(id: string, now = Date.now()): string | null {
  const entry = held.get(id);

  return entry && hasLife(entry.url, entry.expiresOnUtc, now) ? entry.url : null;
}

/** Drops every held URL, for a caller whose session has ended and whose tokens went with it. */
export function forgetAll(): void {
  held.clear();
}
