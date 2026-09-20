# 08 — Cover Images

Status: **In progress** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #2/#13/#14/#24/#26/#29 + "Media storage & delivery" + data-model `activities.CoverImageBlobPath` + "Authentication & authorization".

## Summary

The image that gives an activity a face, and the reason the public list can have pictures at all.
A cover is **its own upload** — never chosen from an activity's private media — and it goes
directly into the container its activity's visibility requires: **`covers` (public)** for a
`Public` activity, **`media` (private, SAS-served)** for a `Shared` or `Private` one
(Decision #29).

That separation is the design's load-bearing rule, and this feature is where it lives: because
there is no code path from a private blob to a cover, promoting private media to public is
impossible by construction rather than by review. The rule used to read "a cover is always
public", which was safe only while every activity was public. It is not safe now — a public cover
for a `Private` activity discloses a picture from an entry the visitor cannot open, and no later
edit can undo it, because a public blob URL cannot be recalled.

Feature 04 owns the `Type` column. This feature owns **what the container does when it changes**.

## Story

As a signed-in user I want to upload a separate cover image for an activity so that anonymous
visitors see a picture beside it without any of the activity's private media becoming public.

## Dependencies

- [04-activity-crud](archive/04-activity-crud.md) (the activity that owns `CoverImageBlobPath`)
- [05-public-activity-list](05-public-activity-list.md) (the list and detail responses that surface the cover URL to anonymous callers)
- [01-foundation](archive/01-foundation.md) (`IStorageRepository` abstraction; the in-memory fake and its `Category=StorageIntegration` tag were deleted on 2026-09-18, and storage now runs against a live account in `TrailBlaze.Repository.Test` under `Category=Container`)

## Acceptance criteria

- [x] `POST /api/activity/{id}/cover` accepts a multipart image upload and returns the resulting cover URL
- [x] The bytes go to the container the activity's **current `Type`** requires (Decision #29): the **public `covers`** container for a `Public` activity, the **private `media`** container for a `Shared` or `Private` one. Asserted **per type**, not once, because a single happy-path check passes even when the routing is inverted for the other two. *(Asserted twice, at the two tiers that can each say half: the ask, by the service tier's recording double, and the outcome, by the container tier's credential-free GET. What each costs is in the test plan below.)*
- [x] The response carries a **plain public URL** for a `Public` activity and a **short-lived SAS URL** for a `Shared`/`Private` one, so the client receives one field either way and never has to know which container holds the bytes
- [x] There is **no** way to nominate an existing private media item as the cover: the request model carries no media id, no blob path, and no reference to a `media` row, and a test asserts that shape
- [x] The content type must be an image on the allowlist (`image/jpeg`, `image/png`, `image/webp`, `image/gif`); a video or unknown type returns 400 with **no blob written**
- [x] Size is capped at 10 MB under the image rule, with a file exactly at the cap accepted and one byte over rejected with no blob write (Decision #24)
- [x] On success `Activity.CoverImageBlobPath` holds the stored path and the response returns the matching URL
- [x] **Replace semantics:** uploading a cover for an activity that already has one replaces the stored path **and** deletes the previous cover blob, so exactly one cover blob exists per activity and none are orphaned. *The "lands in the other container" case is unreachable rather than unhandled, and that is the invariant working: the move below keeps the blob in the container the current `Type` requires, so a replace always finds the previous cover in the container it is writing to. The code derives that container from the entry's type rather than assuming it, so the two would still agree if the move ever stopped running.*
- [x] **Visibility-change move.** When `PUT /api/activity/{id}` changes `Type` across the public line, the cover is moved in the same operation: copied to the destination container, `CoverImageBlobPath` updated, and the blob in the old container **deleted**. `Public` → `Shared`/`Private` must leave **no readable copy** in the public container; the reverse must leave a plain public URL behind
- [x] The move is **required rather than cosmetic**, and this is the criterion that justifies the whole rule: after `Public` → `Private` the old public URL no longer serves the image. The disclosure already happened — that URL may be cached or indexed — so the bytes must go, or a now-`Private` entry's cover stays fetchable by anyone who ever held the link. The test asserts the old blob is **gone**, not merely that the field was repointed
- [x] A `Type` change that does **not** cross the public line (`Shared` ↔ `Private`) moves nothing, and a `Type` change on an activity with **no** cover is a no-op rather than an error
- [x] The move leaves the cover resolvable from **exactly one** container at every observable point — never from none, and never from both. A `Shared`/`Private` activity is never left holding a public-only cover
- [x] A cover upload changes only `CoverImageBlobPath` — title, location, activity date, description, and `Type` are untouched
- [x] An upload against a non-existent activity returns 404; so does an upload to a caller who cannot read the activity, and an anonymous caller gets 401
- [x] The list (feature 05) and the detail response carry the cover URL for an activity that has one, and a null/absent cover for one that does not
- [x] **The cover URL is world-readable only for a `Public` activity.** For `Public`, a plain unauthenticated HTTP GET returns the image (Decision #13); for `Shared`/`Private`, that same plain GET must **fail**, while a freshly minted SAS succeeds. Both directions are asserted, because a container mix-up that left a private cover fetchable would otherwise pass every other criterion here
- [x] The cover upload path shares the private container's **blobs** with media but never creates, modifies, or deletes a `media` **row** — the two are separate concerns living in one container, and the distinction is asserted

## Tests (TDD)

Tiers below are as [testing-and-tdd.md](../testing-and-tdd.md) describes them; `2026-09-18` marks a
bullet whose tier or instrument changed that day, with the operating consequence kept and the
argument for it left to the PR that made the change. The feature's own **tier question is now
settled** — see the first two bullets — and what settled it was feature 05's arrival, not a new
decision here.

**The routing table is asserted in two tiers, and the split is the resolution of the question the
2026-09-18 rewrite left open.** That rewrite was right that a real backend reports nothing about
which container a call named, and wrong that this left routing untestable: a *recording* double
answers the ask even though nothing holds bytes, and `TrailBlaze.Service.Test` already had one —
feature 05 added `RecordingStorage` to assert which container a cover URL was derived from, on the
same reasoning and against the same contract. So the destination container is asserted per type
there, as `covers` for `Public` and `media` for the other two; and the container tier asserts the
half a double structurally cannot, which is that the container a cover was sent to is *public or
private as claimed*, by fetching it with no credentials. Neither tier is the whole rule; the pair is.

- **Hot spot (container routing — test-first)** — `TrailBlaze.Service.Test`, per type rather than once, because a single happy-path check passes when the routing is inverted for the other two: `Public` → `covers`, `Shared` → `media`, `Private` → `media`, read off the upload the recording double saw. **What it cannot show:** that a URL for either container actually works. *(changed 2026-09-18 — the destination as a table over `Type`, hosted against a live account. Now asserted by ask in the service tier, with the outcome asserted in the container tier.)*
- **Hot spot (the move — test-first)** — split the same way: `TrailBlaze.Service.Test` asserts the container pair the service moved *between*, per crossing, and `CoverVisibilityMoveTests` (`TrailBlaze.Repository.Test`, `Category=Container`) asserts that the old public URL stops serving while a fresh SAS succeeds. The no-op half — `Shared` ↔ `Private`, and a `Type` change with no cover — is asserted on the double, where "no move was asked for" is a readable fact: a real backend can only report "nothing changed", which a caller that moved the object and moved it back would also satisfy.
- `TrailBlaze.Service.Test`: the image allowlist accept/reject table including a video type, and the size boundary at exactly 10 MB and 10 MB + 1 byte. *(changed 2026-09-18 — "replacement deletes the previous cover blob" and "a rejected upload leaves the existing cover intact" moved to the container tier and the persistence tier. Both came back with the recording double, which answers them more directly: the delete is asserted as the old path, and the refused replacement as no write at all.)*
- `TrailBlaze.Service.Test`: the URL shape per type — a plain public URL for `Public`, a SAS for `Shared`/`Private` — asserted on the value the service returns, which needs no store because the branch is decided from the activity's `Type` and the string either carries a signature or does not. **What it cannot show** is the container tier's half: a well-formed SAS produced from the wrong key looks identical here and is only refused on fetch.
- Integration (`TrailBlaze.Repository.Test`) — **container-backed** (`Category=Container`): `CoverImageBlobPath` genuinely persists on the activity row, read back through a second context against the tier's migrated database ([testing-and-tdd.md](../testing-and-tdd.md)), and an activity created without a cover has none. The media-row half is asserted in `TrailBlaze.Service.Test` instead, on the writes the service made: a cover path shares the private container but never touches a `media` row, and a write the double recorded is a more direct answer than a query after the fact.
- Integration (`TrailBlaze.Api.Test`): the cover route turns an anonymous caller away, and accepts an id and a file and nothing else — asserted on the action's own signature, because the absence of a parameter is the rule (criterion 4). *(The list and detail JSON carrying the URL is asserted in `TrailBlaze.Service.Test` as a property set rather than through the host: this tier's connection strings point at nothing, so a list response here is a 500 and never a payload.)*
- Storage integration (`TrailBlaze.Repository.Test`, tagged `Category=Container`) — **the move as a real round-trip, and the tier no double can replace:** upload to one container, move across the public line, then assert a credential-free GET against the **old** URL now fails while a fresh SAS succeeds — and the reverse, that arriving in `covers` makes an unsigned URL serve the bytes. A recording double can prove the move was *called* with the right pair; only a real backend proves the bytes are actually unreachable, which is the property the whole rule exists to guarantee — and that gap is why this bullet is not negotiable.
- Storage integration (`TrailBlaze.Repository.Test`, tagged `Category=Container`): **a replaced cover leaves no readable copy** — the old public URL stops serving once the previous blob is deleted. This is the move's failure mode reached by the other route, and it is here for the same reason.

## Notes / non-goals

- **The separation rule, restated for three containers.** A cover is always a fresh upload and is never derived from the activity's private media (Decision #14). Do not "optimise" this by reusing or re-pointing a `media` blob as a cover — that single change is what would make promoting private bytes to public possible, and it is precisely what this feature exists to prevent. What decides a cover's audience is **where it was uploaded**, never a flag on an item (Decision #13, amended by #29). Note the asymmetry the private container now carries: cover *blobs* of `Shared`/`Private` activities sit beside media blobs, but a cover never becomes a `media` **row**, and a `media` row never becomes a cover — asserted in both directions.
- No image resizing, cropping, rotation, or thumbnail generation — bytes are stored exactly as uploaded.
- No cover removal endpoint in v1: replace is the only mutation. (The PRD API surface lists `POST /api/activity/{id}/cover` and no delete route.)
- **No ownership enforcement.** The visibility gate is here and is 404 for an entry the caller cannot read — a cover is the entry's face, so the rule that governs reading it governs giving it one. What is absent is the *mutation* rule: any signed-in caller who **can read** an activity may set its cover, including one they do not own. Feature [09-permission-enforcement](09-permission-enforcement.md) adds the owner/admin rules. Like 06, this feature is therefore part of the deployability gap — see the sequencing note in [00-mission-1-sprint.md](00-mission-1-sprint.md).
- **No request-size override on the route, unlike the media one, and deliberately.** `UploadMedia` raises Kestrel's body limit and the form parser's multipart limit because a 200 MB video exceeds both defaults, and without raising them the documented 400 would arrive as a bare 413. The image cap runs the other way: 10 MB sits *below* Kestrel's 30 MB and the parser's 128 MB, so the defaults already let an oversize image through to the rule that names the reason. Raising them here would buy nothing and buffer more.
- No EXIF or metadata stripping on the uploaded image.
- **Covers belong to activities only** — with one exception added since by Decision #28: user
  **avatars** are a public image, and they live in their own public `avatars` container rather than
  in `covers`. Keeping them apart is what preserves the property the whole split rests on: "is this
  blob public?" stays answerable from the container name alone, with no second question about what
  kind of image it is.
