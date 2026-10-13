# 14 — Every image is stored with a derivative

Status: **Archived** — merged to `develop` in PR #54 · [00-mission-2-sprint.md](../00-mission-2-sprint.md)
Source: [PRD](../../PRD.md) — Decision #31 (new, changed 2026-10-11) + Decision #15 (unchanged,
video-scoped) + "Media storage & delivery" + "Data model"

## Summary

An uploaded image is stored exactly as it arrived and served back at that same size. A 12-megapixel
phone photo is several megabytes, and the grid draws it into a tile a few hundred pixels wide — so
the bytes cross the network, and are decoded by the browser, at a size nobody ever sees.

This feature stores a second, smaller copy of every uploaded image and serves **that** to the
browser. The original is kept and stays retrievable; it is simply no longer what a page loads.

**A cover is the one exception**, and it is the exception because a cover is a tile and nothing else
reads it: the route stores the derivative **alone** and keeps no original. *(2026-10-11 — this is
new. Until then a cover was stored as its original bytes, undecoded.)*

**Video is untouched.** ImageMagick cannot decode video, and this mission does not add an encoder —
so a video is stored as-is, has no derivative, and is delivered exactly as it is today. That is not a
gap being papered over: it is Decision #15, which is scoped to video and is not amended by this
feature.

### There is no reversal here, and the record should not claim one

A first reading of this work suggested it reversed "the image half" of Decision #15. It does not.
That row is titled **Video handling** and has always been about video; no decision ever said images
are stored without transcoding, because the image case was simply never written down. So this feature
**adds** a decision rather than amending one, and #15 is left exactly as it stands. An append-only log
that records a reversal which did not happen is worse than one with a gap in it.

## Story

As someone browsing the journal I want photos to load quickly on a phone, so that a page of images
does not cost me several megabytes of data and several seconds of waiting.

## Dependencies

- [06-media-upload](06-media-upload.md) — the upload path the derivative is produced on
- [07-sas-delivery](07-sas-delivery.md) — the SAS the derivative is served through
- [15-media-url-cache](15-media-url-cache.md) — **implemented first**, out of number order. The
  criterion "the media listing signs the derivative" presumes a listing that signs anything, and
  until 15 landed it did not: the listing carried metadata and the app fetched one URL per item.
  Building the derivative against that would have meant writing the serving path twice, so 15 goes
  first and this feature becomes the switch of which path the listing signs. The two rows in the
  Mission 2 table are out of priority order because of it, and that file says so

## Design

**The package is added to `TrailBlaze.Service` only** — never `TrailBlaze.Model`, which stays free of
third-party types, and never the Api project. `Magick.NET-Q8-AnyCPU`; the `AnyCPU` build carries
native binaries for `linux-x64`, and `src/api/Dockerfile` is Debian-based, so the deployed image
needs nothing added to it.

**The producer is a member of `MediaService`, and both upload paths reach it there.** There is no
type of its own: `GetThumbnailStreamAsync` sits on `IMediaService` / `MediaService` beside the
listing and the mint, and the cover route calls it through the same contract that the media upload
does. *(2026-10-11 — this was `IThumbnailService` / `ThumbnailService` with an options type and a
result record in `TrailBlaze.Model/`, then briefly a singleton `IMediaHelperService` /
`MediaHelperService`. The two value types are gone, the second pair was folded into the media
service, and the remaining contract returns the stream itself; the content type the result carried is
`Constant.Upload.ContentType`.)*

**The quality is read inside the producer, from the process environment, and applied as it stands.**
`ThumbnailQuality` is read through `Environment.GetEnvironmentVariable` on each call — not through
`IConfiguration`, and not in the composition root as the caps and the SAS window are — and its value
becomes the encoder's quality with nothing in between. `Constant.Upload.QualityDefault` and
`QualityCeiling` went with the singleton, and so did the clamp that had used `SignedUrlLifetime` as
its pattern. *(2026-10-13 — three consequences are recorded rather than corrected, because they are
what the code does. An unconfigured process encodes at quality **0**: `Convert.ToUInt32` answers a
null string with zero, and nothing sets the variable, which appears in neither `appsettings.json` nor
`docker-compose.yml`. A value that will not parse **throws** rather than falling back — a
`FormatException` for text, an `OverflowException` for a negative — and the two routes meet that
differently: the media upload swallows it in the derivative's own guard, so the item is stored with
no derivative and nothing backfills one, while the cover route re-raises it and the client sees a
500. And a value above 100 is passed to the encoder rather than clamped.)*

**The derivative is WebP, and it is a re-encode.** The source is decoded, auto-oriented, and written
out as `image/webp` at the applied quality. *(**2026-10-11** — three properties the first design had
are gone, and they are listed here rather than left to be rediscovered:*

- **There is no resize.** The derivative keeps the original's pixel dimensions, so it is a re-encode
  rather than a smaller image. The `maxDimension` setting and its 1600-pixel default are gone with
  it. For a large photograph the saving is the codec's and the quality's, not the pixel count's.
- **There are no decoder resource limits.** The first design set `ResourceLimits` — maximum width,
  height and area, a memory ceiling, a bounded frame list, and no disk — in a static constructor and
  checked the header before decoding. Nothing sets them now, so a decode is bounded by `Magick.NET`'s
  defaults. **This is the feature's one security-relevant reduction**: the bytes are supplied by
  whoever uploads them, and the defence against a small file that expands into a large allocation is
  the one thing that was protecting the container's memory. It is recorded here and in Decision #31
  rather than treated as a detail.
- **The metadata is not stripped.** The first design called `Strip()` after `AutoOrient()`, so a
  derivative carried no EXIF. `AutoOrient()` is still called and still removes the orientation tag —
  the image is not sideways — but the remaining profiles, **GPS included**, are written into the
  derivative. For a **cover** that is a public blob, and a photograph's location travelling with it
  is the kind of thing that should be a decision rather than a consequence.)*

**The derivative lands in the same private `media` container** for activity media, under a path
derived from the original's: the same folder, the same stem, and `Constant.Upload.PathSuffix` +
`Constant.Upload.FileExtension` in place of the extension. A separate container would create a second
answer to "is this blob public?" for no gain. **The suffix is load-bearing rather than cosmetic**: a
derivative of an uploaded WebP would otherwise be named from the stem alone and written onto the
original's own path, and the item would have one blob where its row claims two.

**A cover is stored as the derivative alone**, into the container its activity's `Type` requires
(Decision #29), under a fresh identifier and the derivative's extension. The route decodes what it is
handed, so a file whose bytes are not a decodable image **fails the upload** rather than being stored
as sent — the asymmetry with the media path below, and the cost of keeping no original.

**Which path a route signs.** The media **listing** signs the derivative, so a page loads the smaller
copy. `GET /api/media/{id}/url` keeps signing the **original**, unchanged — so the stored original
stays reachable rather than becoming write-only, which is the only reason storing it has a point.

**A failure to derive is not a failure to upload** — for **media**. If the decode yields nothing, or
the derivative cannot be stored, the item is still stored and still served from its original, and the
derivative's column stays empty. That is deliberately asymmetric with the original's own upload
failure, which keeps the existing undo — the original is the item, and the derivative is an
optimization of it. A cover has no such fallback, for the reason above.

**The bound is on the length the caller declares.** The cap is checked against the `sizeBytes` the
caller passes, and the service does not measure the stream against it. *(**2026-10-11** — the first
design buffered an image through a cap-bounded copy, so a client that declared ten bytes and sent ten
megabytes was refused. The upload takes no such copy now, so that guard is gone with it; what remains
is the route, which passes `IFormFile.Length` — a length the **server** measures — rather than a
length the client states. The claim is withdrawn rather than replaced, and the test that asserted it
is withdrawn with it.)*

**Schema.** `Media` gains `ThumbnailPath`: nullable, `nvarchar(512)`, mirroring `BlobPath`. Nullable
because video and every row that already exists have none.

## Acceptance criteria

- [x] Uploading an **image** stores the original and a derivative, and the derivative's path is
      recorded on the item; uploading a **video** stores exactly what it did before and records no
      derivative
- [x] The derivative is re-encoded, not merely copied, and is **smaller than the original** for a
      typical photograph
- [x] The configured **quality** is applied
- [ ] **The maximum dimension is applied, and an image already inside it is not enlarged** —
      *withdrawn 2026-10-11*: there is no dimension setting and no resize, so there is nothing to
      assert. The criterion is not met and is not being met by a smaller test.
- [x] The derivative lives in the **private** `media` container, and is reachable only by a SAS URL
      minted after the caller has been authorized to read the item's activity — the same gate as the
      original, with no second rule
- [x] The media listing signs the **derivative** when there is one, so a page renders the smaller
      copy; `GET /api/media/{id}/url` still signs the **original**
- [x] An item with no derivative — a video, an older row, a failed decode — is served from its
      original, so nothing that worked before stops working
- [x] An image whose bytes cannot be decoded is still **stored** and its derivative column left
      empty, rather than the upload failing — **for media**. A **cover** is the exception and fails
      instead, because it keeps no original to fall back to
- [x] Deleting a media item removes its derivative as well as its original
- [ ] **A file that exceeds the decoder's resource limits is refused rather than allocated** —
      *withdrawn 2026-10-11*: the limits are no longer set, so no file is refused on those grounds.
      See the Design section; the reduction is recorded rather than papered over.
- [x] The upload's stored bytes are byte-identical to what was sent — the derivative is written to
      its own path and does not replace or mutate the original — **for media**. A cover stores no
      original, so the claim does not apply to it
- [x] The `media` table's new column is nullable and bounded, and the PRD data model states it
- [x] The two paths never collide: an uploaded WebP's derivative does not land on the original's own
      path

## Tests (TDD)

- **Unit — the producer** (in `MediaServiceTests`, which is the class the type belongs to now;
  offline, against the real library, every input built by a hand-written encoder in the test assembly
  rather than by the library under test): **the returned stream is readable after the call returns**
  (the claim that caught the disposed-stream defect the first version of this type shipped); the
  derivative is a WebP however the source was encoded; it comes back smaller than a noisy photograph;
  a transparent source keeps its size; an animated source yields a single frame; bytes that are not an
  image, and an empty file, are **reported rather than returned**, so a caller cannot store an empty
  stream as a success; and the configured quality takes effect. That last one **sets the environment
  variable and restores it**, because reading it is the producer's own job and there is no other way
  in — which is why the class sits in a collection that serialises it against the rest of the
  assembly. The clamp that used to be asserted beside it has nothing left to assert, and is withdrawn
  below.
- **Unit, at the service tier** — the upload path with a recording storage double, which is the one
  sanctioned double and records only whether it was called: an image writes both blobs and records
  the derivative's path, and the derivative is stored as `Constant.Upload.ContentType`; a video writes
  one and records none; an undecodable image still writes the original; a derivative that throws
  leaves the original serving rather than failing the upload; a deletion removes both paths and a
  deletion without a derivative removes one; and a listing signs the derivative while the mint route
  signs the original. A cover is asserted at the same tier: it is stored as the derivative's content
  type whatever the client declared, and it lands in the container the activity's type requires.
- **Model** — the new column's length bound, and that it is **optional** rather than required. The
  model-versus-snapshot comparison regenerates with the migration, so the two move together.
- **Database (container-tagged)** — the migration applies, and an item round-trips its derivative path
  in a fresh scope while an item without one round-trips a null; the path round-trips at the column's
  full width, and one character past it is **refused by the engine**, so the number in the model is a
  bound rather than a comment.
- **Storage (container-tagged)** — an original and its derivative are two objects in the same private
  container, each reached by its own URL with its own content type, and **removing the original leaves
  the derivative fetchable**. That last half is the point: the paths differ by nothing but the suffix,
  so a backend that treated them as one key would make the derivative's write silently replace the
  bytes it was made from, and both writes would still report success.
- **Not reachable from the web tier** — the client change is that it renders whatever URL the listing
  gives it, which it already does; there is no runner in `src/web`, so that half is `verification-only`
  and is observed in a browser rather than asserted.

### Claims withdrawn rather than faked

Four claims are struck rather than met, and the tests that named them went with them. None is a
coverage gap being hidden: the behaviour each named **does not exist**, and a test asserting
otherwise would be an assertion that cannot fail.

- **The maximum-dimension criterion** — the resize is gone, so there is nothing to assert.
- **The decoder-resource-limit criterion** — the limits are gone. This is the one worth re-reading
  before the PR merges: it is the defence this feature's own design notes called the attack surface.
- **The declared-length guard** (`An_image_longer_than_its_declared_length_is_refused_rather_than_held`)
  — the cap-bounded copy it tested is gone with the buffering, and the route cannot produce the
  mismatch: the controller passes `IFormFile.Length`, which the server measures.
- **The quality ceiling and the non-positive fallback** — six cases, withdrawn with the clamp and the
  two constants they read. What replaced them is one assertion that the setting *reaches* the
  encoder, not four about what it is held to; the difference is exactly the clamping that is gone.
  *(2026-10-13 — these are the only claims in this list whose behaviour was removed by a change of
  placement rather than by a decision, and they are recorded here so the next reader does not read
  their absence as an oversight. See the quality paragraph in the Design section for what the code
  does instead.)*

## Notes / non-goals

- **Video gets nothing.** No poster frame, no re-encode, no format change — Decision #15 and the
  PRD's out-of-scope list both stand, and both are about video.
- **An animated image becomes a still.** The derivative is a single frame.
- **Existing images are not backfilled.** A row with no derivative serves its original, which is
  correct and slower. A backfill is a separate piece of work and would mint new paths.
- **The quality is a setting with no default and no ceiling.** *(2026-10-11 — the first design
  defaulted to quality 5, chosen beside a 1600-pixel cap and a JPEG target. With the cap gone and the
  format WebP, 5 encodes the full-size image into something no one would want to look at, so a
  default of 75 was chosen and a ceiling of 100 held it. **2026-10-13** — both went with the
  singleton the setting used to be read in, so what is left is the setting itself, read from the
  environment and applied as it stands: see the quality paragraph in the Design section for what that
  means for a deployment that sets nothing.)*
- **The derivative is not stripped of metadata.** See the Design section: the orientation tag is
  applied and removed, and the rest — GPS included — is carried into the derivative. For a cover this
  is a public blob. Making this a decision rather than a consequence is one line in `MediaService`,
  and it is deliberately left to the review rather than taken here.
- **This is CPU work on the request thread.** The derivative is produced during the upload, so an
  upload is slower than it was; that is the cost of the download being faster.
