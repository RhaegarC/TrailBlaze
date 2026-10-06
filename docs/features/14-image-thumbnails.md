# 14 — Every image is stored with a derivative

Status: **Not started** · [00-mission-2-sprint.md](00-mission-2-sprint.md)
Source: [PRD](../PRD.md) — Decision #31 (new) + Decision #15 (unchanged, video-scoped) + "Media
storage & delivery" + "Data model"

## Summary

An uploaded image is stored exactly as it arrived and served back at that same size. A 12-megapixel
phone photo is several megabytes, and the grid draws it into a tile a few hundred pixels wide — so
the bytes cross the network, and are decoded by the browser, at a size nobody ever sees.

This feature stores a second, smaller copy of every uploaded image and serves **that** to the
browser. The original is kept and stays retrievable; it is simply no longer what a page loads.

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

- [06-media-upload](archive/06-media-upload.md) — the upload path the derivative is produced on
- [07-sas-delivery](archive/07-sas-delivery.md) — the SAS the derivative is served through
- [15-media-url-cache](15-media-url-cache.md) — **implemented first**, out of number order. The
  criterion "the media listing signs the derivative" presumes a listing that signs anything, and
  until 15 landed it did not: the listing carried metadata and the app fetched one URL per item.
  Building the derivative against that would have meant writing the serving path twice, so 15 goes
  first and this feature becomes the switch of which path the listing signs. The two rows in the
  Mission 2 table are out of priority order because of it, and that file says so

## Design

**The package is added to `TrailBlaze.Service` only** — never `TrailBlaze.Model`, which stays free of
third-party types, and never the Api project. `Magick.NET-Q8-AnyCPU`: Q8 is 8 bits per channel, which
is what a JPEG holds, so the extra precision of a Q16 build would be discarded on the way out while
halving nothing but the pixel cache — and the pixel cache is the thing worth halving, because decode
memory is this feature's attack surface. The `AnyCPU` build carries native binaries for `linux-x64`,
and `src/api/Dockerfile` is Debian-based, so the deployed image needs nothing added to it.

**Placement follows the layer rules.** `IThumbnailService` in `TrailBlaze.Interface/Service/`;
`ThumbnailService` in `TrailBlaze.Service/`, carrying that layer's suffix; the options and result
types in `TrailBlaze.Model/`. The options type clamps a configured value the way `SignedUrlLifetime`
clamps its window — **quality defaulting to 5**, and a **maximum dimension defaulting to 1600**
pixels. The dimension is the setting that matters most here: because the derivative is what every
surface receives, including the enlarged view, its pixel size — not its quality — is what decides
whether the enlarged image is legible.

**Decoding is attacker-controlled, and is bounded.** Once an uploaded image is decoded in-process, a
small file can expand into a very large allocation, and the file is supplied by whoever uploads it.
The service therefore sets decoder resource limits **once**, in the type's static constructor —
maximum width, height and area, a memory ceiling, a single-frame list, and essentially no disk, so a
hostile file cannot spill to the container's volume — and checks the header before committing to a
full decode. Width and height are set explicitly because they are the limits the decoders actually
honour, and an area limit alone is not enough to stop a compressed bomb. Keeping this in the static
constructor also means the Api layer names no ImageMagick type and no separate bootstrap type is
introduced into a layer whose files carry the `Service` suffix.

**The upload path needs a second read.** The upload action is handed a forward-only stream, and the
derivative needs the same bytes after the original has been written. The two kinds are already
distinguished, so they part here: an **image** is buffered through a bounded copy — bounded by the
applied image cap, because a declared length must never become the memory a request is allowed to
claim — and the same buffer serves both the original upload and the decode; a **video** is streamed
straight through and never buffered. That asymmetry is why the two caps stay separate settings.

**Schema.** `Media` gains `ThumbnailPath`: nullable, `nvarchar(512)`, mirroring `BlobPath`. Nullable
because video and every row that already exists have none. The PRD data model moves with it, in the
same pull request, as the schema-change discipline requires.

**The derivative lands in the same private `media` container**, under a path derived from the
original's — the same folder, with `-thumb.jpg` in place of its extension. A separate container would
create a second answer to "is this blob public?" for no gain, and the derivative of a private image
is still private. The original's path carries a fresh identifier per upload, so the derivative's path
is unique and its bytes never change beneath it.

**Which path a route signs.** The media **listing** signs the derivative, so a page loads the smaller
copy. `GET /api/media/{id}/url` keeps signing the **original**, unchanged — so the stored original
stays reachable rather than becoming write-only, which is the only reason storing it has a point.

**A failure to derive is not a failure to upload.** If the decode yields nothing, or the derivative
cannot be stored, the item is still stored and still served from its original, and the derivative's
column stays empty. That is deliberately asymmetric with the original's own upload failure, which
keeps the existing undo — the original is the item, and the derivative is an optimization of it.

## Acceptance criteria

- [ ] Uploading an **image** stores the original and a derivative, and the derivative's path is
      recorded on the item; uploading a **video** stores exactly what it did before and records no
      derivative
- [ ] The derivative is re-encoded, not merely copied, and is **smaller than the original** for a
      typical photograph
- [ ] The configured quality and maximum dimension are applied; an image already inside the maximum
      dimension is **not enlarged** to meet it
- [ ] The derivative lives in the **private** `media` container, and is reachable only by a SAS URL
      minted after the caller has been authorized to read the item's activity — the same gate as the
      original, with no second rule
- [ ] The media listing signs the **derivative** when there is one, so a page renders the smaller
      copy; `GET /api/media/{id}/url` still signs the **original**
- [ ] An item with no derivative — a video, an older row, a failed decode — is served from its
      original, so nothing that worked before stops working
- [ ] An image whose bytes cannot be decoded is still **stored** and its derivative column left
      empty, rather than the upload failing
- [ ] Deleting a media item removes its derivative as well as its original
- [ ] A file that exceeds the decoder's resource limits is **refused rather than allocated**, and the
      refusal is not an out-of-memory crash
- [ ] The upload's stored bytes are byte-identical to what was sent — the derivative is produced from
      the buffer, and does not replace or mutate the original
- [ ] The `media` table's new column is nullable and bounded, and the PRD data model states it

## Tests (TDD)

- **Unit** — the derivative producer is a function over bytes and is exercised against the real
  library, offline: a derivative comes back smaller; its long edge respects the configured maximum;
  an image within the maximum is not enlarged; a transparent source becomes a still JPEG; **bytes
  that are not an image yield no derivative rather than an exception**; the configured quality takes
  effect; an animated source yields a single frame. The options type's clamping is asserted beside
  the cap type's, the same way.
- **Unit, at the service tier** — the upload path with a recording storage double, which is the one
  sanctioned double and records only whether it was called: an image writes both blobs and records
  the derivative's path; a video writes one and records none; an undecodable image still writes the
  original; a deletion removes both paths; a listing signs the derivative while the mint route signs
  the original.
- **Model** — the new column's length bound, and that it is **optional** rather than required. The
  model-versus-snapshot comparison regenerates with the migration, so the two move together.
- **Database (container-tagged)** — the migration applies, and an item round-trips its derivative path
  in a fresh scope while an item without one round-trips a null. The migration is additive, so the
  narrowing cases are not affected.
- **Storage (container-tagged)** — the derivative's declared content type survives the round trip
  against the real repository, which is the tier that can tell a declared type from a stored one.
- **Not reachable from the web tier** — the client change is that it renders whatever URL the listing
  gives it, which it already does; there is no runner in `src/web`, so that half is `verification-only`
  and is observed in a browser rather than asserted.

## Notes / non-goals

- **Video gets nothing.** No poster frame, no re-encode, no format change — Decision #15 and the
  PRD's out-of-scope list both stand, and both are about video. An iPhone's HEVC `.mov` is still
  stored faithfully and still will not play in Chrome or Firefox.
- **Metadata is not preserved on the derivative.** Orientation is applied before the derivative is
  written, and the other profiles are dropped — which is the point of a derivative, and means an
  original's metadata is only in the original.
- **An animated image becomes a still.** The derivative is a single frame.
- **Existing images are not backfilled.** A row with no derivative serves its original, which is
  correct and slower. A backfill is a separate piece of work and would mint new paths, which the
  client's URL handling must tolerate.
- **Quality 5 is very lossy and this is a deliberate trade.** It was chosen with the consequence
  stated: a heavy re-encode is visible on a text-heavy image and is what the enlarged view will show.
  Both the quality and the dimension are settings precisely so this can be turned up without a
  rebuild.
- **This is CPU work on the request thread.** The derivative is produced during the upload, so an
  upload is slower than it was; that is the cost of the download being faster.
