# 06 — Media Upload

Status: **Archived** — merged to `develop` in PR #17 · [00-mission-1-sprint.md](../00-mission-1-sprint.md)
Source: [PRD](../../PRD.md) — Decisions #2/#4/#6/#15/#24/#26/#27 + "Media storage & delivery" + data-model `media` table + "Authentication & authorization".

> **Archiving this one does not mean the slice is closed.** It shipped upload, the metadata listing
> and item deletion; fetching the bytes is [07](../07-sas-delivery.md)'s, and two criteria above are
> still open: the administrator among the permitted deleters, which needs a readable role and lands
> with [09](../09-permission-enforcement.md), and the bytes-unmodified claim through the upload route,
> which needs a tier that hosts the API against a live account. `develop` is not deployable until 09
> lands — the sequencing note in the sprint file states why.

## Summary

The first half of the media story: a signed-in user attaches images and videos to an activity, and
those bytes go into the **private** blob container — never the database, never a public container.
This feature owns the validation rules (content-type allowlist, size caps, per-contributor count
cap), the `media` metadata rows, the signed-in metadata listing, and deletion of an item (row +
blob). It deliberately stops at storing the bytes: handing a browser a way to fetch them is feature
07's job.

Media is **collaborative** (Decision #27). The gate is whether the caller can *read* the activity,
not whether they wrote it: any signed-in user who can see an activity may add their own photos and
videos to it, and every row records **who uploaded it** so the detail view can group items by
uploader. This is a deliberate reversal of an "only the author contributes" model, and it is why an
item's `CreatedBy` is a different value from its activity's `CreatedBy` — if they were always the
same, the grouping the product asks for would have exactly one group and be pointless. *(Both are
`EntityBase.CreatedBy`; the item's is written from the caller by this service, the activity's by
feature 04. Corrected 2026-09-20 — they were two columns of their own until the review merged them
into the audit column every `EntityBase` already carries.)*

## Story

As a signed-in user I want to upload images and videos to an activity so that the entry records what
the day actually looked like — including on an activity someone else logged, when I was there too.

## Dependencies

- [04-activity-crud](04-activity-crud.md) (an activity exists to attach media to; its delete is **soft**, so it leaves this feature's rows and blobs standing and a restored activity comes back with its media — *corrected 2026-09-20: the delete used to remove them, which traded a recoverable activity for a permanent loss of its pictures*)
- [05-public-activity-list](05-public-activity-list.md) (the visibility predicate this feature consults to decide whether the caller may contribute)
- [01-foundation](01-foundation.md) (`IStorageRepository` abstraction; the in-memory fake that shipped with it was deleted on 2026-09-18, so storage is now exercised against a live account in `TrailBlaze.Repository.Test` — see [testing-and-tdd.md](../../testing-and-tdd.md))
- [02-entra-auth](02-entra-auth.md) (the caller is authenticated; the endpoints are not anonymous)

## Acceptance criteria

- [x] `POST /api/activity/{id}/media` accepts a multipart form upload and returns 201 on success
- [x] The upload is allowed to **any signed-in caller who can read the activity**, not only its
      creator — including a `User` uploading to a stranger's `Public` or `Shared` activity
      (Decision #27). The check is the visibility rule, so it is the same predicate feature 05
      applies, consulted through the shared authorization service rather than re-derived here
- [x] A caller who **cannot read** the activity is refused with **404**, not 403 — a `Private`
      activity they do not own, which they must not even be able to detect. No blob is written and
      no row is created on that path
- [x] An anonymous caller gets **401** before any blob operation
- [x] The content type must be on the allowlist (images `image/jpeg`, `image/png`, `image/webp`, `image/gif`; videos `video/mp4`, `video/quicktime`); anything else returns 400 and **no blob is written**
- [x] Image size cap is 10 MB: a file exactly at the cap is accepted, one byte over is rejected with 400 and no blob write (Decision #24)
- [x] Video size cap is 200 MB, with the same at-cap-accepted / over-cap-rejected boundary (Decision #24)
- [x] `Kind` is derived from the content type — `image/*` → `Image`, `video/*` → `Video` — and is stored on the row
- [x] A contributor may add at most 50 media items to one activity, enforced against **that contributor's existing** rows there — not the activity's total, since media is collaborative (Decision #27) and a shared budget would let one person fill the entry: an upload at the cap returns a conflict (409) and writes no blob
- [x] The count check and the insert are not racy — a concurrent pair of uploads at the boundary cannot leave one contributor over their cap
- [x] On success a `media` row persists with `ActivityId`, **`CreatedBy`**, `Kind`, `BlobPath`, `ContentType`, `SizeBytes`, `OriginalFileName`, and `CreatedOn`; `SizeBytes` equals the bytes actually stored and `OriginalFileName` is kept for display only
- [x] `CreatedBy` is taken from the **caller**, never from the request body — a client cannot attribute an upload to someone else, exactly as an activity's creator cannot. The response still names the field `UploadedByUserId`, because that is what it means to a client; the column is the shared audit one
- [x] The blob is written to the **private** container through `IStorageRepository`; this endpoint never writes to the public container
- [x] A rejection (bad type, oversize, count exceeded) leaves no blob and no row — no partial state
- [ ] Blob bytes are stored unmodified, including video — the stored length and content hash match the uploaded file (Decision #15). **Half met:** unmodified bytes are asserted for the storage implementation in the container tier, but not through the upload route — no tier drives the upload route against a live account (**unchanged from the 2026-09-18 note** in the tests below), so "the bytes this route stored are the bytes it was sent" is still a code-reading claim
- [x] `GET /api/activity/{id}/media` returns metadata only (`Id`, `Kind`, `ContentType`, `SizeBytes`, `OriginalFileName`, `CreatedOn`, **`UploadedByUserId`**, and the **uploader's display name**) for a caller who can read the activity, and exposes no value that is directly fetchable without a SAS
- [x] The uploader's display name is resolved by joining `users`, so the client can group and label items without a request per uploader — the field the detail view groups on is part of this response, not something the client assembles
- [x] `GET /api/activity/{id}/media` returns **404** to a caller who cannot read the activity, and **401** to an anonymous one, so the media surface cannot be used to probe for a `Private` entry's existence
- [x] `DELETE /api/media/{id}` deletes the blob and the row; an unknown id returns 404
- [ ] Deletion is permitted to **three** principals and no others (Decision #27): the item's **uploader**, the **owner of the activity it belongs to**, and an **admin**. A fourth signed-in user gets 403 and an anonymous caller gets 401. All three permitted paths are asserted, not only the uploader's — the activity owner's right to curate their own entry is the one that is easy to leave untested. **Two of the three are implemented and asserted** — the uploader and the activity's owner — and the **admin is blocked, not skipped**: nothing can read a role, so an administrator is judged as an ordinary user and the branch lands with [09](../09-permission-enforcement.md). Both behaviours are pinned by a test that asserts today's answer, so the day a role arrives that test fails and points at the branch to write *(corrected 2026-09-24 — [09](../09-permission-enforcement.md) settled it as **two** principals: the admin branch landed, and the activity's owner was **removed** from the list at review. Owning an entry carries no right to remove media another user uploaded to it, and this criterion's "activity owner's right to curate their own entry" is the rule that was rejected. The PRD row [Decision #27](../../PRD.md) was narrowed in the same change.)*
- [x] `DELETE /api/activity/{id}` leaves that activity's media rows and blobs **untouched, whichever uploader contributed each one** — the delete is soft and recoverable, so the media has to outlive it and come back with the activity *(corrected 2026-09-20 — the delete path removed the media, which made an undoable activity delete destroy pictures that could not be recovered; a test pins the new answer and the repository tier asserts that a media row survives an activity's deletion)*
- [x] `GET /api/activity/{id}/media` against a non-existent activity returns 404

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) **hot spot (upload validation — must be test-first):** the content-type allowlist as an accept/reject table including a video type sent as an image and vice versa, and the size boundary at exactly the cap and cap+1 for both kinds — both are decided from the request alone and need no store. **Changed 2026-09-18:** the count cap at 19/20/21 is *not* this bullet's any more: counting the activity's existing items is a query, and with no fake database it needs real rows. **Resolved 2026-09-20:** that boundary now runs in the container tier, where the rows are real (the bullet below), and this tier keeps the half that is a decision rather than a count — that a store answering "no room" produces a refusal with **no blob written**, counted against `Constant.MediaLimit.PerContributorPerActivity` and against *this* contributor on *this* activity rather than either alone *(the predicate the service built is compiled and run against the contributor's own row, a second contributor's row on the same activity, and the contributor's row on another activity, so all three axes are executed rather than described — corrected 2026-09-20, when the cap moved from per-activity to per-contributor-per-activity)*. For a rejection path, a **purpose-built recording double defined inside that one test** — it implements `IStorageRepository` only to record whether it was invoked, exists for no other test, and stands in for no storage behaviour. It supports the single step "the service short-circuited before calling storage"; it cannot support "no orphan blob", which is a statement about a store it never talks to — that needs the real `AzureBlobStorageRepository` and a container that can be listed afterwards, and the same wiring as the count boundary.
- Unit (`TrailBlaze.Service.Test`): `Kind` derivation; and that `CreatedBy` — the uploader — comes from the caller, with a payload-supplied uploader ignored — both computed from the request, no store involved. *(changed 2026-09-18 — the assertion that the container asked for was the private one is withdrawn, because a real backend does not report which container a call asked for.)* **The stronger form replaces it:** the container the service names is asserted as `Constant.StorageContainer.Media` on the call it makes, and the bytes really being *in* the private container is the storage tier's claim, below.
- Unit (`TrailBlaze.Service.Test`) — **hot spot (access control):** the contribution gate as a table over {anonymous, non-owner who can read, non-owner who cannot read, owner} × {`Public`, `Shared`, `Private`}, asserting 401 / 201 / 404 / 201 respectively, and that a refused upload invokes **no** `IStorageRepository` write — the purpose-built recording double described above, which is the one claim a storage double is still sanctioned for. Deletion is a second table over the same principals against {uploader, activity owner, other signed-in user, anonymous}. **The admin row is blocked, not skipped** *(2026-09-20 — it was in both tables' line-up and cannot be answered: a role is not readable anywhere, so an administrator is judged as an ordinary user. Rather than drop the row, each table carries a test that asserts today's answer and names the branch 09 has to write, so the missing rule fails loudly instead of passing silently.)*
- Unit (`TrailBlaze.Service.Test`) — deleting an activity **leaves its media alone**: the delete is soft, so the items have to come back with it. The double records a delete by type and answers no media *read* at all, so a cascade that came back would either fail on the read or land in `DeletedMedia` and fail the assertion, whichever leg it re-entered by. *(added 2026-09-20 — replacing the bullet that asserted the cascade's order, which is what the review asked to remove.)*
- Integration (`TrailBlaze.Repository.Test`): the media listing's join to `users` resolves the uploader's display name, and the uploader is asserted to be `EntityBase.CreatedBy`, the shared audit column, rather than a column of the item's own. **There is no FK to assert:** the model declares no foreign keys at all, so the uploader reference is a plain value and the join to `users` is a query-time join, not a navigation *(corrected 2026-09-18 — the same fact retired the cascade row from [testing-and-tdd.md](../../testing-and-tdd.md); the column identity corrected 2026-09-20)*. The listing's name resolution is asserted in the service tier against a recording repository, where the join is a decision; the model tier pins the bounded columns, their nullability, the `bigint` size, the `CK_Media_Kind` check constraint, the index on `ActivityId`, and the absence of relationships, all read off the design-time model with no connection.
- Integration (`TrailBlaze.Repository.Test`) — **container-backed** (`Category=Container`): `media` rows genuinely persist. The tier starts `azure-sql-edge`, gives the run one migrated database, and reads the rows back through a second context, so the save interception's string key and `CreatedOn` stamp are asserted against the database instead of against the change tracker that just wrote them ([testing-and-tdd.md](../../testing-and-tdd.md)). *(changed 2026-09-18 — "with no database behind them", and with it "before a connection opens": the unreachable-connection harness could only show that the interceptor had *staged* something.)* Two clauses follow from the absent FK: the count query is still inspected with `ToQueryString()` where the claim is about the statement, but the **activity → media relationship cannot be asserted from the EF model because there is no foreign key** — what an activity's deletion does to its media is the service's decision plus the schema's silence, and it is proven against the engine or not at all; and `SizeBytes`/`ContentType`/`OriginalFileName` now round-trip through the mapping against that engine. **Added 2026-09-20:** the cap's boundary (one under the cap lands, the next is refused) and the race at it, a second contributor admitted to an activity one contributor has filled, a second activity left unfilled by the first, a removed row freeing its place, a 200 MB size surviving its column, the `bigint` the migration actually created, SQL Server rejecting a kind outside the closed set, and — asserted against the engine rather than read off the service — **an activity's deletion leaving its media rows live**. **The race is the one test in this feature that fails if the isolation level is dropped** — verified by running it against `ReadCommitted` and watching it produce rows over the cap.
- Integration (`TrailBlaze.Api.Test`): **corrected 2026-09-20 — the multipart 400s are not reachable here.** Every media route carries `[Authorize]`, so without a token the actions are never entered: a request with no file part, an oversize one and a disallowed one all answer **401** before the binder's work is reached. What this tier *can* assert is where the three routes live and that they turn an anonymous caller away, which it does. **The 201 and the at-the-cap upload's 409 are proven nowhere** *(2026-09-18 — "against the fake storage" is withdrawn, with nothing to put in its place)*. `TrailBlaze.Api.Test` boots the real host with an unreachable database and a placeholder storage account, so any request that reaches the count query or a blob write fails by design, and no tier hosts the API against the containers. The outcome half — the row persists, the bytes land in the private container — is covered in `TrailBlaze.Repository.Test` against the real engine and the emulator; the HTTP multipart contract above it is a stated gap rather than a papered-over one, and closing it needs a token this tier does not have ([09](../09-permission-enforcement.md) is where that arrives).
- Storage integration (`TrailBlaze.Repository.Test`, tagged `Category=Container`; `dotnet test --filter "Category=Container"`): the real `AzureBlobStorageRepository` against a live account — upload to the private container, read the bytes back unmodified, delete, and confirm the content type round-trips. *(changed 2026-09-18 — this bullet had it backwards: it used to sit in `TrailBlaze.Service.Test` under a deleted `Category=StorageIntegration` tag and to skip without credentials, which made the storage double the thing that ran.)* The tier now runs **by default** against the Azurite emulator, which needs no credentials, and against a real account only when `TRAILBLAZE_STORAGE_CONNECTION` names one; it **skips, never fails**, when nothing answers. There is no fake left to be proven; the implementation is what these assertions are about.

## Notes / non-goals

- **No transcoding, no thumbnails, no poster frames** (PRD Decision #15). Stated honestly: an iPhone recording survives as an HEVC `.mov` and is stored faithfully, but it **will not play in Chrome or Firefox** — it is not converted, so it simply will not render there. That is an accepted consequence, not a bug to fix here.
- **Collaborative upload is the rule, not a staging state.** Any caller who can read the activity may
  add media — that is Decision #27, and feature [09-permission-enforcement](../09-permission-enforcement.md)
  does not change it. What 09 retrofits is the **administrator**, named as a principal for both
  contributing and deleting and impossible to recognize until a role can be read; the uploader is
  already implemented, the activity's owner is implemented for deleting and was then **removed** from
  that list at 09's review (see the dated correction above), and the visibility rule itself is this feature's, applied
  on every read and write here through `IActivityService`'s `CanRead`. *(corrected 2026-09-20 — this
  paragraph said the feature "ships without any visibility check of its own", which contradicted the
  acceptance criteria above it and the PRD, whose wording is unambiguous: "any signed-in user who can
  see an activity may add media to it". The criteria and the PRD are the rule; the sentence was
  stale.)*
- **No per-item visibility control.** Visibility is per **activity** (Decision #26), never per item:
  there is no "make this video public" flag, and an item's audience is its activity's. Media also
  always requires sign-in, whatever the activity's `Type` (Decision #2).
- **The detail view's "folders" are presentation grouping, not storage.** Items are grouped by
  `UploadedByUserId` at render time; there is no `media.FolderId`, no user-created album, and nothing
  to persist. Collapsing a group is client-side state, not a write.
- No resumable or chunked upload, no background job — one request, capped at 200 MB.
- No deduplication, no virus scanning, no EXIF/metadata stripping, no media captions or reordering.
- No media in the anonymous payload — that boundary belongs to feature [05-public-activity-list](05-public-activity-list.md) and stays closed here.
