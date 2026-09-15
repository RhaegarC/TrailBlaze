# 08 — Cover Images

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
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

- [04-activity-crud](04-activity-crud.md) (the activity that owns `CoverImageBlobPath`)
- [05-public-activity-list](05-public-activity-list.md) (the list and detail responses that surface the cover URL to anonymous callers)
- [01-foundation](01-foundation.md) (`IStorageService` abstraction + the in-memory fake; the tagged storage tier)

## Acceptance criteria

- [ ] `POST /api/activities/{id}/cover` accepts a multipart image upload and returns the resulting cover URL
- [ ] The bytes go to the container the activity's **current `Type`** requires (Decision #29): the **public `covers`** container for a `Public` activity, the **private `media`** container for a `Shared` or `Private` one. The `IStorageService` fake records the container asked for, so the wrong container is a test failure — asserted **per type**, not once
- [ ] The response carries a **plain public URL** for a `Public` activity and a **short-lived SAS URL** for a `Shared`/`Private` one, so the client receives one field either way and never has to know which container holds the bytes
- [ ] There is **no** way to nominate an existing private media item as the cover: the request model carries no media id, no blob path, and no reference to a `media` row, and a test asserts that shape
- [ ] The content type must be an image on the allowlist (`image/jpeg`, `image/png`, `image/webp`, `image/gif`); a video or unknown type returns 400 with **no blob written**
- [ ] Size is capped at 10 MB under the image rule, with a file exactly at the cap accepted and one byte over rejected with no blob write (Decision #24)
- [ ] On success `Activity.CoverImageBlobPath` holds the stored path and the response returns the matching URL
- [ ] **Replace semantics:** uploading a cover for an activity that already has one replaces the stored path **and** deletes the previous cover blob, so exactly one cover blob exists per activity and none are orphaned — including when the replacement lands in the *other* container
- [ ] **Visibility-change move.** When `PUT /api/activities/{id}` changes `Type` across the public line, the cover is moved in the same operation: copied to the destination container, `CoverImageBlobPath` updated, and the blob in the old container **deleted**. `Public` → `Shared`/`Private` must leave **no readable copy** in the public container; the reverse must leave a plain public URL behind
- [ ] The move is **required rather than cosmetic**, and this is the criterion that justifies the whole rule: after `Public` → `Private` the old public URL no longer serves the image. The disclosure already happened — that URL may be cached or indexed — so the bytes must go, or a now-`Private` entry's cover stays fetchable by anyone who ever held the link. The test asserts the old blob is **gone**, not merely that the field was repointed
- [ ] A `Type` change that does **not** cross the public line (`Shared` ↔ `Private`) moves nothing, and a `Type` change on an activity with **no** cover is a no-op rather than an error
- [ ] The move leaves the cover resolvable from **exactly one** container at every observable point — never from none, and never from both. A `Shared`/`Private` activity is never left holding a public-only cover
- [ ] A cover upload changes only `CoverImageBlobPath` — title, location, activity date, description, and `Type` are untouched
- [ ] An upload against a non-existent activity returns 404; so does an upload to a caller who cannot read the activity, and an anonymous caller gets 401
- [ ] The list (feature 05) and the detail response carry the cover URL for an activity that has one, and a null/absent cover for one that does not
- [ ] **The cover URL is world-readable only for a `Public` activity.** For `Public`, a plain unauthenticated HTTP GET returns the image (Decision #13); for `Shared`/`Private`, that same plain GET must **fail**, while a freshly minted SAS succeeds. Both directions are asserted, because a container mix-up that left a private cover fetchable would otherwise pass every other criterion here
- [ ] The cover upload path shares the private container's **blobs** with media but never creates, modifies, or deletes a `media` **row** — the two are separate concerns living in one container, and the distinction is asserted

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) **hot spot (container routing — test-first):** the destination container as a table over `Type` — `covers` for `Public`, `media` for `Shared` and `Private` — with the fake `IStorageService` recording which container it was asked for, so any mix-up is a failure. Asserted per type rather than once, because a single happy-path check passes even when the routing is inverted for the other two.
- Unit (`TrailBlaze.Service.Test`) **hot spot (the move — test-first):** `Public` → `Private` copies into the private container **and deletes the public blob**, asserted as two calls on the fake with the delete asserted explicitly; the reverse direction likewise. Also that `Shared` ↔ `Private` moves nothing, and that a `Type` change with no cover does no storage work at all.
- Unit (`TrailBlaze.Service.Test`): the image allowlist accept/reject table including a video type; the size boundary at exactly 10 MB and 10 MB + 1 byte; replacement deletes the previous cover blob and stores the new path; a rejected upload leaves the existing cover and its blob intact.
- Unit (`TrailBlaze.Service.Test`): the URL shape per type — a plain public URL for `Public`, a SAS for `Shared`/`Private` — asserted against the fake so the branching is covered without a network call.
- Integration (`TrailBlaze.Repository.Test`): with no database, `CoverImageBlobPath` persists on the activity row through the `DbContext` (the reads are inspected with `ToQueryString()`), and a cover upload leaves the activity's `media` rows unchanged ([testing-and-tdd.md](../testing-and-tdd.md)).
- Integration (`TrailBlaze.Api.Test`): the anonymous list and detail JSON carry the cover URL for an activity that has one; an activity without a cover returns null/absent rather than an error.
- Storage integration (`TrailBlaze.Service.Test`, tagged `Category=StorageIntegration`): a real round-trip against the public container — upload a `Public` activity's cover, then fetch the returned URL with a **credential-free** HTTP GET and receive 200 with the original bytes. That anonymous fetch is the proof the container is genuinely public-read, which the fake cannot demonstrate.
- Storage integration (`TrailBlaze.Service.Test`, tagged `Category=StorageIntegration`) — **the move as a real round-trip, and the tier that cannot be faked:** upload a `Public` cover, change the activity to `Private`, then assert a credential-free GET against the **old** URL now fails while a fresh SAS succeeds. The fake can prove the delete was *called*; only real Azure proves the bytes are actually unreachable, which is the property the whole rule exists to guarantee.

## Notes / non-goals

- **The separation rule, restated for three containers.** A cover is always a fresh upload and is never derived from the activity's private media (Decision #14). Do not "optimise" this by reusing or re-pointing a `media` blob as a cover — that single change is what would make promoting private bytes to public possible, and it is precisely what this feature exists to prevent. What decides a cover's audience is **where it was uploaded**, never a flag on an item (Decision #13, amended by #29). Note the asymmetry the private container now carries: cover *blobs* of `Shared`/`Private` activities sit beside media blobs, but a cover never becomes a `media` **row**, and a `media` row never becomes a cover — asserted in both directions.
- No image resizing, cropping, rotation, or thumbnail generation — bytes are stored exactly as uploaded.
- No cover removal endpoint in v1: replace is the only mutation. (The PRD API surface lists `POST /api/activities/{id}/cover` and no delete route.)
- **No ownership or visibility enforcement.** Any signed-in user can currently set the cover on any activity, including one they cannot read; feature [09-permission-enforcement](09-permission-enforcement.md) adds the owner/admin rules and the visibility gate (404 for a `Private` activity the caller cannot see). Like 06, this feature builds mechanics while everyone may still write anything, so it is **not safe to deploy** before 09.
- No EXIF or metadata stripping on the uploaded image.
- **Covers belong to activities only** — with one exception added since by Decision #28: user
  **avatars** are a public image, and they live in their own public `avatars` container rather than
  in `covers`. Keeping them apart is what preserves the property the whole split rests on: "is this
  blob public?" stays answerable from the container name alone, with no second question about what
  kind of image it is.
