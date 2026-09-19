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

- [04-activity-crud](04-activity-crud.md) (an activity exists to attach media to, and deleting one must take its media with it — service work, not a schema cascade: the model declares no foreign keys)
- [05-public-activity-list](05-public-activity-list.md) (the visibility predicate this feature consults to decide whether the caller may contribute)
- [01-foundation](archive/01-foundation.md) (`IStorageRepository` abstraction; the in-memory fake that shipped with it was deleted on 2026-09-18, so storage is now exercised against a live account in `TrailBlaze.Repository.Test` — see [testing-and-tdd.md](../testing-and-tdd.md))
- [02-entra-auth](archive/02-entra-auth.md) (the caller is authenticated; the endpoints are not anonymous)

## Acceptance criteria

- [ ] `POST /api/activity/{id}/media` accepts a multipart form upload and returns 201 on success
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
- [ ] `GET /api/activity/{id}/media` returns metadata only (`Id`, `Kind`, `ContentType`, `SizeBytes`, `OriginalFileName`, `CreatedOn`, **`UploadedByUserId`**, and the **uploader's display name**) for a caller who can read the activity, and exposes no value that is directly fetchable without a SAS
- [ ] The uploader's display name is resolved by joining `users`, so the client can group and label items without a request per uploader — the field the detail view groups on is part of this response, not something the client assembles
- [ ] `GET /api/activity/{id}/media` returns **404** to a caller who cannot read the activity, and **401** to an anonymous one, so the media surface cannot be used to probe for a `Private` entry's existence
- [ ] `DELETE /api/media/{id}` deletes the blob and the row; an unknown id returns 404
- [ ] Deletion is permitted to **three** principals and no others (Decision #27): the item's **uploader**, the **owner of the activity it belongs to**, and an **admin**. A fourth signed-in user gets 403 and an anonymous caller gets 401. All three permitted paths are asserted, not only the uploader's — the activity owner's right to curate their own entry is the one that is easy to leave untested
- [ ] `DELETE /api/activity/{id}` cascades to that activity's media rows **regardless of uploader**, and the stored blobs are cleaned up — no foreign key exists, so this is work the delete path does rather than a constraint the schema enforces — an owner deleting their activity is never blocked by media someone else contributed
- [ ] `GET /api/activity/{id}/media` against a non-existent activity returns 404

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) **hot spot (upload validation — must be test-first):** the content-type allowlist as an accept/reject table including a video type sent as an image and vice versa, and the size boundary at exactly the cap and cap+1 for both kinds — both are decided from the request alone and need no store. **Changed 2026-09-18:** the count cap at 19/20/21 is *not* this bullet's any more: counting the activity's existing items is a query, and with no fake database it needs real rows, so that boundary is the one claim in this section whose tier is still unsettled ([item 25](../tech-debt/25-service-test-tier-is-empty.md)) — either the service tier is wired to an engine, or the boundary is not run at all. For a rejection path, a **purpose-built recording double defined inside that one test** — it implements `IStorageRepository` only to record whether it was invoked, exists for no other test, and stands in for no storage behaviour. It supports the single step "the service short-circuited before calling storage"; it cannot support "no orphan blob", which is a statement about a store it never talks to — that needs the real `AzureBlobStorageRepository` and a container that can be listed afterwards, and the same wiring as the count boundary above.
- Unit (`TrailBlaze.Service.Test`): `Kind` derivation; and that `UploadedByUserId` comes from the caller, with a payload-supplied uploader ignored — both computed from the request, no store involved. *(changed 2026-09-18 — the assertion that the container asked for was the private one is withdrawn, because a real backend does not report which container a call asked for.)* **The stronger form replaces it:** after an upload the bytes are *in* the private container, which is where a `covers` write would not have put them.
- Unit (`TrailBlaze.Service.Test`) — **hot spot (access control):** the contribution gate as a table over {anonymous, non-owner who can read, non-owner who cannot read, owner, admin} × {`Public`, `Shared`, `Private`}, asserting 401 / 201 / 404 / 201 / 201 respectively, and that a refused upload invokes **no** `IStorageRepository` write — the purpose-built recording double described above, which is the one claim a storage double is still sanctioned for. Deletion is a second table over the same principals against {uploader, activity owner, other signed-in user, admin, anonymous}.
- Integration (`TrailBlaze.Repository.Test`): the media listing's join to `users` resolves the uploader's display name, and `UploadedByUserId` is asserted as a mapped column with its type and length. **There is no FK to assert:** the model declares no foreign keys at all, so `UploadedByUserId` is a plain column and the join to `users` is a query-time join, not a navigation *(corrected 2026-09-18; the same fact retired the cascade row from [testing-and-tdd.md](../testing-and-tdd.md))*. The model tier still inspects the generated SQL with `ToQueryString()` and opens no connection, which proves the statement; the display name coming *back* is an executed result and belongs to the container tier.
- Integration (`TrailBlaze.Repository.Test`) — **container-backed** (`Category=Container`): `media` rows genuinely persist. The tier starts `azure-sql-edge`, gives the run one migrated database, and reads the rows back through a second context, so the save interception's string key and `CreatedOn` stamp are asserted against the database instead of against the change tracker that just wrote them ([testing-and-tdd.md](../testing-and-tdd.md)). *(changed 2026-09-18 — "with no database behind them", and with it "before a connection opens": the unreachable-connection harness could only show that the interceptor had *staged* something.)* Two clauses follow from the absent FK: the per-activity count query is still inspected with `ToQueryString()` where the claim is about the statement, but the **activity → media cascade cannot be asserted from the EF model because there is no foreign key** — removing an activity's media is work the service does, and it is proven against the engine or not at all; and `SizeBytes`/`ContentType`/`OriginalFileName` now round-trip through the mapping against that engine.
- Integration (`TrailBlaze.Api.Test`): a multipart request through the real pipeline on the paths that end before a store is reached — an oversize and a disallowed-type request returning 400. **The 201 and the 21st upload's 409 are proven nowhere** *(2026-09-18 — "against the fake storage" is withdrawn, with nothing to put in its place)*. `TrailBlaze.Api.Test` boots the real host with an unreachable database and a placeholder storage account, so any request that reaches the count query or a blob write fails by design, and no tier hosts the API against the containers. The outcome half — the row persists, the bytes land in the private container — is covered in `TrailBlaze.Repository.Test` against the real engine and the emulator; the HTTP multipart contract above it is a stated gap rather than a papered-over one.
- Storage integration (`TrailBlaze.Repository.Test`, tagged `Category=Container`; `dotnet test --filter "Category=Container"`): the real `AzureBlobStorageRepository` against a live account — upload to the private container, read the bytes back unmodified, delete, and confirm the content type round-trips. *(changed 2026-09-18 — this bullet had it backwards: it used to sit in `TrailBlaze.Service.Test` under a deleted `Category=StorageIntegration` tag and to skip without credentials, which made the storage double the thing that ran.)* The tier now runs **by default** against the Azurite emulator, which needs no credentials, and against a real account only when `TRAILBLAZE_STORAGE_CONNECTION` names one; it **skips, never fails**, when nothing answers. There is no fake left to be proven; the implementation is what these assertions are about.

## Notes / non-goals

- **No transcoding, no thumbnails, no poster frames** (PRD Decision #15). Stated honestly: an iPhone recording survives as an HEVC `.mov` and is stored faithfully, but it **will not play in Chrome or Firefox** — it is not converted, so it simply will not render there. That is an accepted consequence, not a bug to fix here.
- **Collaborative upload is the rule, not a staging state.** Any caller who can read the activity may
  add media — that is Decision #27, and feature [09-permission-enforcement](09-permission-enforcement.md)
  does not close it. What 09 retrofits is the other half: refusing a caller who *cannot* read the
  activity, and the uploader/owner/admin rule for deletion. This feature still ships without any
  visibility check of its own, so it is part of the deployability gap — see the sequencing note in
  [00-mission-1-sprint.md](00-mission-1-sprint.md).
- **No per-item visibility control.** Visibility is per **activity** (Decision #26), never per item:
  there is no "make this video public" flag, and an item's audience is its activity's. Media also
  always requires sign-in, whatever the activity's `Type` (Decision #2).
- **The detail view's "folders" are presentation grouping, not storage.** Items are grouped by
  `UploadedByUserId` at render time; there is no `media.FolderId`, no user-created album, and nothing
  to persist. Collapsing a group is client-side state, not a write.
- No resumable or chunked upload, no background job — one request, capped at 200 MB.
- No deduplication, no virus scanning, no EXIF/metadata stripping, no media captions or reordering.
- No media in the anonymous payload — that boundary belongs to feature [05-public-activity-list](05-public-activity-list.md) and stays closed here.
