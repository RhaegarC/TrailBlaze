# 15 — A listing carries its URLs, and the browser may keep them

Status: **Not started** · [00-mission-2-sprint.md](00-mission-2-sprint.md)
Source: [PRD](../PRD.md) — Decision #7 (amended) + Decision #32 (new) + Decision #30 + "Reading —
what a caller receives"

## Summary

Three separate things make a page of images expensive to render, and each one is fixed here.

**The listing does not carry its URLs.** `GET /api/activity/{id}/media` returns metadata only, so the
app follows it with one `GET /api/media/{id}/url` **per item** — a listing of twenty items is
twenty-one requests. The fix is to mint the URLs in the listing itself.

**Nothing is cached.** No blob carries a `Cache-Control`, so a browser has no permission to reuse
bytes it already holds, and re-visiting a page re-downloads every image.

**A re-minted URL is a different URL.** A SAS carries its expiry, so minting twice produces two
different strings — which means that even a browser willing to cache would treat the second listing
as a page of new images. Caching a signed URL is only possible if the URL is stable, so stability is
part of this feature rather than a refinement of it.

### The PRD already describes the first of these

The reading matrix and the paragraph beneath it already say an anonymous caller on a `Public` entry
receives "a short-lived SAS URL per item". That is a description of the listing, and the listing does
not currently do it — the app assembles the URLs itself, one request at a time. So this feature is
the code catching up to what is written down, and the PRD sentence is re-read for precision as part
of it rather than changed.

## Story

As someone scrolling a journal of photos I want a page to load once and then be instant when I come
back to it, instead of re-downloading every image on every visit.

## Dependencies

- [07-sas-delivery](archive/07-sas-delivery.md) — the minting rule this feature extends to the listing
- [12-anonymous-media-read](archive/12-anonymous-media-read.md) — the anonymous listing this serves

## Design

**Minting in the listing is cheap, which is why it belongs there.** Signing is computed locally — no
network round trip, no blob operation — so signing a page's worth of URLs costs a loop, and it
replaces a client round trip per item. Decision #30 already permits a SAS URL in this payload, so
nothing about the anonymous surface changes to allow it.

**The response field is named for what it is.** `Url`, holding a signed or public URL, and an
`ExpiresOnUtc` beside it. It is deliberately not named for the blob, and it is deliberately not the
path: no response carries a blob path, and a name that read like one would undermine a claim the
suite asserts.

**`GET /api/media/{id}/url` stays.** It is what refreshes a URL that has lapsed. No route is added
and none is removed, so the anonymous surface remains exactly the four read endpoints the matrix
lists — and that is a criterion here, not a coincidence, because widening it was the subject of a
separate decision.

**Stability, and the rounding that gives it.** The expiry is already rounded down to the whole second
"which is the precision a SAS carries". This feature rounds the minting instant down to a **one-minute
boundary** before the window is added, so two mints inside the same minute produce the same string and
a browser's cache key does not move. The cost is stated rather than hidden: a URL minted late in a
boundary minute lives slightly less than the configured window — still comfortably inside the cap, and
the cap is unchanged.

**Caching is granted, and bounded.** Uploads currently set a content type and nothing else. They gain
a cache directive: media bytes are `private` with a short lifetime, so a browser may reuse what it
already holds and a shared proxy may not store it at all.

**The tension is real and is written down rather than argued away.** A SAS is a bearer token, and the
PRD says the expiry window is the real control. A browser permitted to reuse bytes for five minutes
will re-display a private image after its token has lapsed — that is what the window *means*. What
bounds it: the window is far shorter than the shortest possible SAS lifetime, the directive is
`private` so no intermediary keeps a copy, and caching grants nobody new access — it lets the same
browser re-show what it already fetched. The window is the knob to tighten first if it is judged too
long, and it is the one number here that trades performance directly against retention.

**The client half, and why no seam marker is needed.** Every file that changes — the wire types, the
mapper, the media hook, and a new URL cache — is hand-written rather than part of the Figma export,
so the export-comparison gate never sees them and no `@integration` marker applies. `App.tsx` is not
touched: it renders whatever URL it is handed and already does so.

**A service worker is rejected, deliberately.** Blob responses are cross-origin, so the Cache API
would store opaque responses it can neither inspect nor validate; and a cache directive on a now-stable
URL already gives durable reuse without a service worker's versioned lifecycle to get wrong.

## Acceptance criteria

- [ ] A media listing carries a read URL and an expiry for **every** item, minted from a single expiry
      for the page rather than one instant per item
- [ ] The app requests a listing **once** and makes no per-item URL request, so a page of images costs
      one request rather than one plus the number of items
- [ ] Two listings minted within the same boundary minute produce **identical** URLs for the same
      item, so a browser's cache key does not move between visits
- [ ] The SAS lifetime cap is unchanged, and a URL minted at a boundary still has a life inside it —
      the rounding shortens a URL's life by less than one boundary, never lengthens it
- [ ] Media bytes are served with a **`private`** cache directive and a bounded lifetime; the
      directive is not `public`, not `immutable`, and no media URL is treated as content-stable
- [ ] The app **honours the expiry it is given**: a URL with time left is reused rather than re-minted,
      and one close to lapsing is refreshed before it is used
- [ ] A listing still carries **no** blob path and no uploader user id for an anonymous caller
      (Decision #30) — the new field is a URL, and the existing payload assertions are extended rather
      than relaxed
- [ ] **No route is added or removed**: the anonymous surface is still the same four read endpoints,
      and the media-URL mint route still works and still refuses an unreadable item with 404
- [ ] An item whose URL has lapsed renders again once refreshed, rather than breaking the page

## Tests (TDD)

- **Unit** — the expiry rule: two mints inside a boundary share one instant, a mint on either side of
  a boundary does not, and the result is still rounded to what a SAS can carry. The listing's
  signing behaviour is asserted beside the existing mint assertions, so the "one mint method" claim
  keeps holding: both paths reach the same repository call rather than each rolling its own.
- **Unit, on the payload** — the serialized listing carries the URL and the expiry, still carries no
  blob path, and an anonymous listing still carries no user id. These are the assertions that make the
  new field safe to add, so they are extended in the same change rather than after it.
- **Host** — no route was added or removed: the anonymous reachability assertion is the anchor, and it
  is expected to pass **unchanged**. A change to it would mean the design drifted.
- **Storage (container-tagged)** — a cache directive given on upload is readable back off the real
  blob, and an upload given none leaves the property unset. This needs the real repository: a
  declared header is not a stored header until something stores it.
- **Not assertable here** — the browser's own reuse is a client behaviour. There is no runner in
  `src/web`, so "the second visit did not re-download" is `verification-only`: observed in a browser's
  network panel against a running stack, and recorded as what was seen rather than claimed as tested.

## Notes / non-goals

- **This is not a CDN or a reverse proxy.** The PRD's deployment section says there is no reverse
  proxy, and this feature does not add one — the cache is the browser's own, bounded by the directive.
- **Private covers are out of scope for the media listing change.** They are minted on the read paths
  that already mint them, and their requirement to be moved when an activity's visibility changes is
  unchanged; that is why covers are explicitly not marked content-stable.
- **No per-item visibility is introduced**, and none is implied by caching one item's URL separately
  from another's.
- **The expiry reported is the expiry signed.** A caller told a URL is valid until an instant is
  holding a token that carries that same instant — which is why the value is on the payload at all,
  and why the client is expected to use it rather than ignore it.
- **Existing clients that ignore the new field keep working**: an extra field is additive, and the
  mint route they already call is still there.
