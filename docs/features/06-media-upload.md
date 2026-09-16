# 06 — Media Upload

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #2/#4/#6/#15/#24/#26/#27 + "Media storage & delivery" + data-model `media` table + "Authentication & authorization".

## Summary

The first half of the media story: a signed-in user attaches images and videos to an activity, and
those bytes go into the **private** blob container — never the database, never a public container.
This feature owns the validation rules (content-type allowlist, size caps, per-activity count cap),
the `media` metadata rows, the signed-in metadata listing, and deletion of an item (row + blob). It
deliberately stops at storing the bytes: handing a browser a way to fetch them is feature 07's job.

Media is **collaborative** (Decision #27). The gate is whether the caller can *read* the activity,
not whether they wrote it: any signed-in user who can see an activity may add their own photos and
videos to it, and every row records **who uploaded it** so the detail view can group items by
uploader. This is a deliberate reversal of an "only the author contributes" model, and it is why
`media.UploadedByUserId` is a different column from `activities.CreatedByUserId` — if they were the
same, the grouping the product asks for would have exactly one group and be pointless.

## Story

As a signed-in user I want to upload images and videos to an activity so that the entry records what
the day actually looked like — including on an activity someone else logged, when I was there too.

## Dependencies

- [04-activity-crud](04-activity-crud.md) (an activity exists to attach media to, and its FK gives the cascade delete)
- [05-public-activity-list](05-public-activity-list.md) (the visibility predicate this feature consults to decide whether the caller may contribute)
- [01-foundation](archive/01-foundation.md) (`IStorageRepository` abstraction + the in-memory fake used by unit tests)
- [02-entra-auth](02-entra-auth.md) (the caller is authenticated; the endpoints are not anonymous)

## Acceptance criteria

- [ ] `POST /api/activities/{id}/media` accepts a multipart form upload and returns 201 on success
- [ ] The upload is allowed to **any signed-in caller who can read the activity**, not only its
      creator — including a `User` uploading to a stranger's `Public` or `Shared` activity
      (Decision #27). The check is the visibility rule, so it is the same predicate feature 05
      applies, consulted through the shared authorization service rather than re-derived here
- [ ] A caller who **cannot read** the activity is refused with **404**, not 403 — a `Private`
      activity they do not own, which they must not even be able to detect. No blob is written and
      no row is created on that path
- [ ] An anonymous caller gets **401** before any blob operation
- [ ] The content type must be on the allowlist (images `image/jpeg`, `image/png`, `image/webp`, `image/gif`; videos `video/mp4`, `video/quicktime`); anything else returns 400 and **no blob is written**
- [ ] Image size cap is 10 MB: a file exactly at the cap is accepted, one byte over is rejected with 400 and no blob write (Decision #24)
- [ ] Video size cap is 200 MB, with the same at-cap-accepted / over-cap-rejected boundary (Decision #24)
- [ ] `Kind` is derived from the content type — `image/*` → `Image`, `video/*` → `Video` — and is stored on the row
- [ ] A maximum of 20 media items per activity is enforced against the activity's **existing** row count — counting items from **all** uploaders, since media is collaborative (Decision #27): an upload at 20 returns a conflict (409) and writes no blob
- [ ] The count check and the insert are not racy — a concurrent pair of uploads at the boundary cannot leave 21 rows
- [ ] On success a `media` row persists with `ActivityId`, **`UploadedByUserId`**, `Kind`, `BlobPath`, `ContentType`, `SizeBytes`, `OriginalFileName`, and `CreatedOn`; `SizeBytes` equals the bytes actually stored and `OriginalFileName` is kept for display only
- [ ] `UploadedByUserId` is taken from the **caller**, never from the request body — a client cannot attribute an upload to someone else, exactly as `CreatedByUserId` works on the activity
- [ ] The blob is written to the **private** container through `IStorageRepository`; this endpoint never writes to the public container
- [ ] A rejection (bad type, oversize, count exceeded) leaves no blob and no row — no partial state
- [ ] Blob bytes are stored unmodified, including video — the stored length and content hash match the uploaded file (Decision #15)
- [ ] `GET /api/activities/{id}/media` returns metadata only (`Id`, `Kind`, `ContentType`, `SizeBytes`, `OriginalFileName`, `CreatedOn`, **`UploadedByUserId`**, and the **uploader's display name**) for a caller who can read the activity, and exposes no value that is directly fetchable without a SAS
- [ ] The uploader's display name is resolved by joining `users`, so the client can group and label items without a request per uploader — the field the detail view groups on is part of this response, not something the client assembles
- [ ] `GET /api/activities/{id}/media` returns **404** to a caller who cannot read the activity, and **401** to an anonymous one, so the media surface cannot be used to probe for a `Private` entry's existence
- [ ] `DELETE /api/media/{id}` deletes the blob and the row; an unknown id returns 404
- [ ] Deletion is permitted to **three** principals and no others (Decision #27): the item's **uploader**, the **owner of the activity it belongs to**, and an **admin**. A fourth signed-in user gets 403 and an anonymous caller gets 401. All three permitted paths are asserted, not only the uploader's — the activity owner's right to curate their own entry is the one that is easy to leave untested
- [ ] `DELETE /api/activities/{id}` cascades to that activity's media rows (FK cascade) **regardless of uploader**, and the stored blobs are cleaned up — an owner deleting their activity is never blocked by media someone else contributed
- [ ] `GET /api/activities/{id}/media` against a non-existent activity returns 404

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) **hot spot (upload validation — must be test-first):** the content-type allowlist as an accept/reject table including a video type sent as an image and vice versa; the size boundary at exactly the cap and cap+1 for both kinds; the count cap at 19/20/21. The fake `IStorageRepository` records every call, so a rejection test asserts the fake was **never invoked** for a write — proving no orphan blob.
- Unit (`TrailBlaze.Service.Test`): `Kind` derivation; the private container name being the one requested (a covers write here is a test failure). Also that `UploadedByUserId` comes from the caller and that a payload-supplied uploader is ignored.
- Unit (`TrailBlaze.Service.Test`) — **hot spot (access control):** the contribution gate as a table over {anonymous, non-owner who can read, non-owner who cannot read, owner, admin} × {`Public`, `Shared`, `Private`}, asserting 401 / 201 / 404 / 201 / 201 respectively, and that a refused upload invokes **no** `IStorageRepository` write. Deletion is a second table over the same principals against {uploader, activity owner, other signed-in user, admin, anonymous}.
- Integration (`TrailBlaze.Repository.Test`): the media listing's join to `users` resolves the uploader's display name, and the `UploadedByUserId` FK is asserted from the EF model — inspected with `ToQueryString()`, with no database ([testing-and-tdd.md](../testing-and-tdd.md)).
- Integration (`TrailBlaze.Repository.Test`): `media` rows persist with no database behind them — the save interception assigns the string key and stamps `CreatedOn` before a connection opens, the per-activity count query is inspected with `ToQueryString()`, the activity → media cascade is asserted from the EF model, and `SizeBytes`/`ContentType`/`OriginalFileName` are carried through the mapping ([testing-and-tdd.md](../testing-and-tdd.md)).
- Integration (`TrailBlaze.Api.Test`): a multipart request end-to-end against the fake storage, 201 plus a metadata body; an oversize and a bad-type request returning 400; the 21st upload returning 409.
- Storage integration (`TrailBlaze.Service.Test`, tagged `Category=StorageIntegration`): the real Azure implementation — upload to the private container, read the bytes back unmodified, delete, confirm the content type round-trips. Without credentials in CI this tier is skipped, so the fake is proven but the blob implementation is not.

## Notes / non-goals

- **No transcoding, no thumbnails, no poster frames** (PRD Decision #15). Stated honestly: an iPhone recording survives as an HEVC `.mov` and is stored faithfully, but it **will not play in Chrome or Firefox** — it is not converted, so it simply will not render there. That is an accepted consequence, not a bug to fix here.
- **Collaborative upload is the rule, not a staging state.** Any caller who can read the activity may
  add media — that is Decision #27, and feature [09-permission-enforcement](09-permission-enforcement.md)
  does not close it. What 09 retrofits is the other half: refusing a caller who *cannot* read the
  activity, and the uploader/owner/admin rule for deletion. This feature still ships without any
  visibility check of its own, so it remains **not safe to deploy** before 09 — the mechanics stay
  independently testable in the meantime.
- **No per-item visibility control.** Visibility is per **activity** (Decision #26), never per item:
  there is no "make this video public" flag, and an item's audience is its activity's. Media also
  always requires sign-in, whatever the activity's `Type` (Decision #2).
- **The detail view's "folders" are presentation grouping, not storage.** Items are grouped by
  `UploadedByUserId` at render time; there is no `media.FolderId`, no user-created album, and nothing
  to persist. Collapsing a group is client-side state, not a write.
- No resumable or chunked upload, no background job — one request, capped at 200 MB.
- No deduplication, no virus scanning, no EXIF/metadata stripping, no media captions or reordering.
- No media in the anonymous payload — that boundary belongs to feature [05-public-activity-list](05-public-activity-list.md) and stays closed here.
