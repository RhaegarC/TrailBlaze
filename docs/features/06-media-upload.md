# 06 — Media Upload

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #2/#4/#6/#15/#24 + "Media storage & delivery" + data-model `media` table + "Authentication & authorization".

## Summary

The first half of the media story: a signed-in user attaches images and videos to an activity, and
those bytes go into the **private** blob container — never the database, never the public
container. This feature owns the validation rules (content-type allowlist, size caps, per-activity
count cap), the `media` metadata rows, the signed-in metadata listing, and deletion of an item
(row + blob). It deliberately stops at storing the bytes: handing a browser a way to fetch them is
feature 07's job.

## Story

As a signed-in user I want to upload images and videos to an activity so that the entry records what
the day actually looked like.

## Dependencies

- [04-activity-crud](04-activity-crud.md) (an activity exists to attach media to, and its FK gives the cascade delete)
- [01-foundation](01-foundation.md) (`IStorageService` abstraction + the in-memory fake used by unit tests)
- [02-entra-auth](02-entra-auth.md) (the caller is authenticated; the endpoints are not anonymous)

## Acceptance criteria

- [ ] `POST /api/activities/{id}/media` accepts a multipart form upload and returns 201 on success
- [ ] The content type must be on the allowlist (images `image/jpeg`, `image/png`, `image/webp`, `image/gif`; videos `video/mp4`, `video/quicktime`); anything else returns 400 and **no blob is written**
- [ ] Image size cap is 10 MB: a file exactly at the cap is accepted, one byte over is rejected with 400 and no blob write (Decision #24)
- [ ] Video size cap is 200 MB, with the same at-cap-accepted / over-cap-rejected boundary (Decision #24)
- [ ] `Kind` is derived from the content type — `image/*` → `Image`, `video/*` → `Video` — and is stored on the row
- [ ] A maximum of 20 media items per activity is enforced against the activity's **existing** row count: an upload at 20 returns a conflict (409) and writes no blob
- [ ] The count check and the insert are not racy — a concurrent pair of uploads at the boundary cannot leave 21 rows
- [ ] On success a `media` row persists with `ActivityId`, `Kind`, `BlobPath`, `ContentType`, `SizeBytes`, `OriginalFileName`, and `CreatedUtc`; `SizeBytes` equals the bytes actually stored and `OriginalFileName` is kept for display only
- [ ] The blob is written to the **private** container through `IStorageService`; this endpoint never writes to the public container
- [ ] A rejection (bad type, oversize, count exceeded) leaves no blob and no row — no partial state
- [ ] Blob bytes are stored unmodified, including video — the stored length and content hash match the uploaded file (Decision #15)
- [ ] `GET /api/activities/{id}/media` returns metadata only (`Id`, `Kind`, `ContentType`, `SizeBytes`, `OriginalFileName`, `CreatedUtc`) for a signed-in caller, and exposes no value that is directly fetchable without a SAS
- [ ] `DELETE /api/media/{id}` deletes the blob and the row; an unknown id returns 404
- [ ] `DELETE /api/activities/{id}` cascades to that activity's media rows (FK cascade), and the stored blobs are cleaned up
- [ ] `GET /api/activities/{id}/media` against a non-existent activity returns 404

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) **hot spot (upload validation — must be test-first):** the content-type allowlist as an accept/reject table including a video type sent as an image and vice versa; the size boundary at exactly the cap and cap+1 for both kinds; the count cap at 19/20/21. The fake `IStorageService` records every call, so a rejection test asserts the fake was **never invoked** for a write — proving no orphan blob.
- Unit (`TrailBlaze.Service.Test`): `Kind` derivation; the private container name being the one requested (a covers write here is a test failure).
- Integration (`TrailBlaze.Repository.Test`): `media` rows persist with their FK; the per-activity count query returns the right number; deleting an activity cascades its media rows; `SizeBytes`/`ContentType`/`OriginalFileName` round-trip through SQL Server.
- Integration (`TrailBlaze.Api.Test`): a multipart request end-to-end against the fake storage, 201 plus a metadata body; an oversize and a bad-type request returning 400; the 21st upload returning 409.
- Storage integration (`TrailBlaze.Service.Test`, tagged `Category=StorageIntegration`): the real Azure implementation — upload to the private container, read the bytes back unmodified, delete, confirm the content type round-trips. Without credentials in CI this tier is skipped, so the fake is proven but the blob implementation is not.

## Notes / non-goals

- **No transcoding, no thumbnails, no poster frames** (PRD Decision #15). Stated honestly: an iPhone recording survives as an HEVC `.mov` and is stored faithfully, but it **will not play in Chrome or Firefox** — it is not converted, so it simply will not render there. That is an accepted consequence, not a bug to fix here.
- **No ownership enforcement.** Every signed-in user can currently upload to and delete media on any activity; feature [09-permission-enforcement](09-permission-enforcement.md) imposes the owner/admin rules on top. This feature is therefore **not safe to deploy** (see the sprint's sequencing note) — the mechanics are built to be independently testable and 09 states the permission matrix as its own criteria.
- No per-media visibility control. The public/private split is per **class**, not per item (Decisions #13/#14); there is no "make this video public" flag.
- No resumable or chunked upload, no background job — one request, capped at 200 MB.
- No deduplication, no virus scanning, no EXIF/metadata stripping, no media captions or reordering.
- No media in the anonymous payload — that boundary belongs to feature [05-public-activity-list](05-public-activity-list.md) and stays closed here.
