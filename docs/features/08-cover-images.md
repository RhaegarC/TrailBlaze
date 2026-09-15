# 08 — Cover Images

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #2/#13/#14/#24 + "Media storage & delivery" + data-model `activities.CoverImageBlobPath` + "Authentication & authorization".

## Summary

The one image in the product that anonymous visitors may see, and the reason the public list can
have pictures at all. The cover is **its own upload** into the **public** container — never chosen
from an activity's private media. That separation is the design's load-bearing rule: because there
is no code path from a private blob to a cover, promoting private media to public is impossible by
construction rather than by review.

## Story

As a signed-in user I want to upload a separate cover image for an activity so that anonymous
visitors see a picture beside it without any of the activity's private media becoming public.

## Dependencies

- [04-activity-crud](04-activity-crud.md) (the activity that owns `CoverImageBlobPath`)
- [05-public-activity-list](05-public-activity-list.md) (the list and detail responses that surface the cover URL to anonymous callers)
- [01-foundation](01-foundation.md) (`IStorageService` abstraction + the in-memory fake; the tagged storage tier)

## Acceptance criteria

- [ ] `POST /api/activities/{id}/cover` accepts a multipart image upload and returns the resulting public cover URL
- [ ] The bytes are written to the **public** `covers` container through `IStorageService`; the private container is never written by this endpoint
- [ ] There is **no** way to nominate an existing private media item as the cover: the request model carries no media id, no blob path, and no reference to a `media` row, and a test asserts that shape
- [ ] The content type must be an image on the allowlist (`image/jpeg`, `image/png`, `image/webp`, `image/gif`); a video or unknown type returns 400 with **no blob written**
- [ ] Size is capped at 10 MB under the image rule, with a file exactly at the cap accepted and one byte over rejected with no blob write (Decision #24)
- [ ] On success `Activity.CoverImageBlobPath` holds the public blob path and the response returns the corresponding public URL
- [ ] **Replace semantics:** uploading a cover for an activity that already has one replaces the stored path **and** deletes the previous cover blob, so exactly one cover blob exists per activity and none are orphaned
- [ ] A cover upload changes only `CoverImageBlobPath` — title, location, activity date, and description are untouched
- [ ] An upload against a non-existent activity returns 404
- [ ] The anonymous list (feature 05) and the anonymous detail response both carry the cover URL for an activity that has one, and a null/absent cover for one that does not
- [ ] The cover URL is a plain public blob URL: no SAS is minted for it, and a plain unauthenticated HTTP GET against it returns the image (Decision #13)
- [ ] The cover upload path does not create, modify, or delete any `media` row

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) **hot spot (upload validation + container separation — test-first):** the image allowlist accept/reject table including a video type; the size boundary at exactly 10 MB and 10 MB + 1 byte; and the container assertion — the fake `IStorageService` records the container it was asked for, so a test fails if the private container is used, and a second asserts the private container is never touched by this path.
- Unit (`TrailBlaze.Service.Test`): replacement deletes the previously stored cover blob and stores the new path; a rejected upload leaves the existing cover and its blob intact.
- Integration (`TrailBlaze.Repository.Test`): with no database, `CoverImageBlobPath` persists on the activity row through the `DbContext` (the reads are inspected with `ToQueryString()`), and a cover upload leaves the activity's `media` rows unchanged ([testing-and-tdd.md](../testing-and-tdd.md)).
- Integration (`TrailBlaze.Api.Test`): the anonymous list and detail JSON carry the cover URL for an activity that has one; an activity without a cover returns null/absent rather than an error.
- Storage integration (`TrailBlaze.Service.Test`, tagged `Category=StorageIntegration`): a real round-trip against the public container — upload, then fetch the returned URL with a **credential-free** HTTP GET and receive 200 with the original bytes. That anonymous fetch is the proof the container is genuinely public-read, which the fake cannot demonstrate.

## Notes / non-goals

- **The two-container separation is the rule.** A cover is always a fresh upload into the public container and is never derived from the activity's private media (PRD Decision #14). Do not "optimise" this by reusing or re-pointing a `media` blob as a cover — that single change is what would make promoting private bytes to public possible, and it is precisely what this feature exists to prevent. The cover is public because of *where it was uploaded*, not because of a flag on an item (Decision #13).
- No image resizing, cropping, rotation, or thumbnail generation — bytes are stored exactly as uploaded.
- No cover removal endpoint in v1: replace is the only mutation. (The PRD API surface lists `POST /api/activities/{id}/cover` and no delete route.)
- **No ownership enforcement.** Any signed-in user can currently set the cover on any activity; feature [09-permission-enforcement](09-permission-enforcement.md) adds the owner/admin rules. Like 06, this feature builds mechanics while everyone may still write anything, so it is **not safe to deploy** before 09.
- No EXIF or metadata stripping on the uploaded image.
- No profile or avatar images — covers belong to activities only, and there is no other public image class.
