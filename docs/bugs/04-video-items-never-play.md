# 04 — A video item is listed and can never be played

Status: **Open** — fix in progress on `fix/04-video-items-never-play` · [00-bug-log.md](00-bug-log.md)
Reported: 2026-10-16 · Severity: **High** — every video on every activity is unplayable, and nothing
is exposed
Component: `src/web` — the activity detail's media section

## Symptom

An activity's Photos & Videos section shows its images and never plays its videos. A video is a row: a
play triangle, the file name, `size · content-type`, and the words **SAS required**. No player is
offered, on the row or anywhere else, so there is nothing to press and nothing plays. Images in the
same section are unaffected.

## Cause — the row renders the metadata and drops the URL

`ActivityDetail` in `src/web/src/App.tsx` renders the two kinds in two branches, and only one of them
uses the item's URL:

```tsx
<button ... onClick={() => setLightboxIndex(globalIdx)}>
  <img src={m.url} alt={m.originalFileName} ... />
</button>
...
{groupVideos.map((m) => (
  <div key={m.id} className={`flex items-center gap-4 px-4 py-3 border ${C.border} mb-2 mt-2`}>
    ...
    <span ...>{t.sasRequired}</span>
  </div>
))}
```

`src/web/src` contains no `<video>` element at all; the only occurrences of the word are the upload
form's `accept="image/*,video/*"` and its `file.type.startsWith("video/")` kind check. The video branch
is a metadata render, `m.url` is read nowhere in it, and the badge is the visible half of that — it
tells the reader a SAS is required at the moment the app is holding one.

**Nothing upstream of the render is missing.** The URL is fetched, carried and thrown away at the last
step:

- `useActivityMedia` (`src/web/src/data/hooks.ts`) runs every item — video included — through
  `withFreshUrl`, so a video's `MediaView` carries the same live URL an image's does, and for the same
  signing window.
- `MediaService.ListAsync` (`src/api/TrailBlaze.Service/MediaService.cs`) builds a response for every
  row with no kind filter, and `ToResponseAsync` signs `media.ThumbnailPath ?? media.BlobPath`. A video
  records no derivative (`A_video_is_stored_once_and_records_no_derivative`), so the URL it is handed is
  its own original video blob, with the content type the upload stored.

So the server answers with a playable URL for a video, the client holds it, and the render never puts it
anywhere.

## It is a recorded export gap, reported as a defect

Feature 10 audited this and recorded it as **criterion 6, unmet**, under *Known gaps in the current
export*:

> **Videos do not play** — criterion 6. `ActivityDetail` renders a video as a filename-and-size row with
> a "SAS required" badge and **no `<video>` element** … For a video the same URL is fetched and sits
> unused in the item's `url`, so the data is wired and nothing renders it.

Its stated policy for such a gap is that it is "fixed in Figma Make and re-exported rather than patched
in `src/web/`". **This fix departs from that policy**, deliberately, and the departure is the thing to
review:

- **The app is what the reporter is using, and a re-export is not available to this fix.** Criterion 6
  stays unmet in the export either way; this closes it in the app.
- **The patch is additive and small.** The row's own markup, styling and metadata line are kept; a
  player is placed above them. That is the same kind of change feature 10 already made in `src/web` for
  the padlock it removed and the cover button it made carry a file input, both behind seam markers.
- **The divergence is recorded rather than hidden.** The render sits between `@integration:begin` and
  `@integration:end`, so `scripts/web-seam.py` reads it as integration rather than as drift.

**The export is not edited, so the correction is still owed upstream.** A re-export replaces
`src/web/src/App.tsx` with the export's copy and this player goes with it —
[`src/web/UPSTREAM.md`](../../src/web/UPSTREAM.md) is the procedure, and Figma Make is where the
`<video>` element belongs.

## Fix

- `src/web/src/App.tsx` — the video branch renders `<video src={m.url} controls preload="metadata">`
  above the unchanged metadata row, inside the seam markers. `preload="metadata"` because the URL
  addresses a private blob and a listing should not pull a whole video down before anyone presses play;
  a max height so a portrait phone recording does not fill the viewport.
- **The player is guarded on `m.url`.** `withFreshUrl` answers an empty string when a single mint fails,
  and an empty `src` is a player reporting an error rather than a row reporting its file. With no URL,
  the row is what it is today minus the badge that was not true.
- `sasRequired` is **deleted** from both locale tables rather than left with no reader, as
  `signInToView` was when the padlock went.

## Verification

**There is no regression test, because `src/web` has no test tier** — no runner in
`src/web/package.json`, the position bugs 01, 02 and 03 record. The change is `verification-only`:

- `npx tsc --noEmit` — clean.
- `npm run build` — clean.
- `python scripts/web-seam.py` — unchanged from `develop`: the two pre-existing failures in
  `src/web/.gitignore` and `src/web/.figma/make/site.json`, and nothing new. The edited block is
  inside its seam markers, which is what keeps this change out of the drift report.
- Manual, signed in — an activity holding a video plays it. This is the observable claim the report is
  about, and it is the only check here that could show the fix works.

The server half is already asserted — `A_listing_signs_the_derivative_when_there_is_one` and
`An_item_with_no_derivative_is_served_from_its_original` are what make `m.url` a playable URL for a
video, and both were passing throughout. What no tier covers is the element the URL is put into: the
seam between the client and the server, which is where this bug lived.

## Not covered here

- **Nothing verifies the player, now or later.** No gate in this repository can see a `<video>` render
  and no tier exercises `src/web`, so "a video plays" rests on the manual pass above.
- **A URL that lapses while the page is open is not re-minted for the player.** The URL is minted at
  page load — the hook fetches once per mount — so a detail page left open past the signing window
  holds a URL that no longer resolves, and the player has no retry. Images carry the same exposure
  today, and a reload clears both.
- **The video is served at its upload size.** Mission 2's feature 14 derives a smaller copy for images
  and leaves video alone deliberately, so this changes which element renders the bytes and not how many
  of them there are.
- **The export still holds the gap**, so a re-export reintroduces this bug unless the export is
  corrected first. `python scripts/web-seam.py --plan` is what names that work.
