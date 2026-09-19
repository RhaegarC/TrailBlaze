# PRD — TrailBlaze Activity Journal

Status: **Draft** (captured from requirements-grilling session, 2026-09-15)
Owner: TBD

## Vision

An **activity journal** with per-entry visibility. Anyone can browse the list of **Public**
activities ordered by date descending — title, location, date, and a cover image. Signing in
reveals the media those activities carry and adds the entries their owners marked **Shared**
(signed-in users only). Entries marked **Private** are visible to their owner alone.

TrailBlaze is one **shared journal**, not a set of private diaries: everyone posts to the same
feed, and the visibility control exists so that some entries can be held back rather than so that
each user gets their own. Visibility decides who may *read* an entry; ownership decides who may
*edit* it. Media is collaborative — a signed-in user who can see an activity may add their own
photos and videos to it, and each item records who uploaded it.

## Stakeholders

- **Visitor (anonymous)** — browses the list of **Public** activities and reads their text; sees
  their cover images; cannot see activity media, cannot see Shared or Private entries, and
  cannot post.
- **User (signed in)** — everything a visitor can do, plus: sees **Shared** entries and their
  own **Private** ones; views every visible activity's images and videos; creates activities;
  adds media to any activity they can see; edits or deletes their own; and maintains their own
  profile (display name, bio, avatar, theme, language).
- **Admin (signed in)** — everything a user can do, plus: sees, edits, or deletes *any*
  activity regardless of visibility. Has no separate screens; the role is purely elevated rights
  in the same UI.

## System overview

| Component | Tech | Responsibility |
|---|---|---|
| Web app | React 19 (Vite) + TypeScript + Tailwind, exported from **Figma Make** | Single role-gated app: the public activity list, activity detail with media, and the create/edit form. Auth via MSAL. |
| Backend | ASP.NET Core 10 | REST + OAuth 2.0 bearer; activities CRUD; media upload; SAS URL issuance; authn/authz |
| Database | **Azure SQL Server** | Users, activities, media metadata. Binary media lives in Azure Blob, never in the DB. |
| Blob storage | Azure Blob Storage | Three containers, each with one security story: **public** `covers` (covers of Public activities), **public** `avatars` (user avatars), **private** `media` (activity media, plus covers of Shared and Private activities). |
| Identity | Entra ID | Sign-in, bearer tokens, role claims |

The backend is a layered solution under `src/api/` — `TrailBlaze.Model`, `TrailBlaze.Repository`,
`TrailBlaze.Service`, `TrailBlaze.Interface`, `TrailBlaze.Api` — each layer carrying a sibling
xUnit test project. The solution file is `src/api/TrailBlaze.slnx`; the test projects are
`TrailBlaze.Api.Test`, `TrailBlaze.Repository.Test`, and `TrailBlaze.Service.Test`. Test command:
`dotnet test` (from `src/api/`).

Data conventions — application-assigned string GUID keys, soft delete, and the audit trail — are
specified in [src/api/STANDARD.md](../src/api/STANDARD.md) §3 and §10, and enforced by
`EntityBase` plus the save interceptor. This PRD states *what* the data holds; STANDARD.md states
*how* it is shaped.

Deployment: the API runs on **Azure Container Apps** from the image `src/api/Dockerfile` builds;
the web app runs on **Azure Static Web Apps**, deployed by GitHub workflow. There is no
`docker-compose.yml` — neither target consumes a multi-service local stack, so there is none.
Azure SQL Database, Azure Blob and Entra ID are **real cloud resources** in every *deployed*
environment — the API never points at a local stand-in where it runs.

**Scoped 2026-09-19: the database half of that holds for deployed environments only, and a
developer's `dotnet run` may point at a SQL Edge container on loopback.** The API is not deployed
anywhere that can reach one, so this changes what a laptop exercises and not what a release does.
The decision is recorded here rather than in a commit message because it narrows a claim this
paragraph used to make without qualification, and it is bounded on purpose: the local database is
a schema to develop against, not a substitute for the managed engine that ships, and the
differences in version, collation and certificate are not covered by developing against it. Azure
Blob and Entra ID stay real in every environment the application runs in — the local database is
the whole of the exception. [STANDARD.md](../src/api/STANDARD.md) §6 carries the rules.

**The test tier is the exception for tests, and it is deliberate (Decisions #5 and #6).** `dotnet test`
runs against containers started by `src/api/docker-compose.test.yml` — SQL Edge and Azurite —
because a test that asserts against a fake asserts about the fake. That file starts **no
application process**; it is not the local multi-service stack the sentence above rules out.

### Current state vs. target

This PRD describes the **target** product. The code is built up to it feature by feature
(`docs/features/`), so the two differ today. Each difference is a deliberate, sequenced gap — not
drift — and each is tracked below. Anything not listed here is expected to match the code.

**Built ahead of the ladder.** The repository started from a generic layered .NET scaffold, so
some work the ladder attributes to features 01–02 already exists:

| Already in the code | Where |
|---|---|
| Entra ID bearer-token validation, wired only when `TenantId` and `Audience` are both configured; anonymous otherwise, with a startup warning saying so | `TrailBlaze.Api/ServiceExt.cs`, `Program.cs` |
| Caller auto-provisioning — `GET /user/me` inserts the caller's row on first authenticated call | `UserService.GetOrCreateAsync`, `UserController` |
| `GET /health` and an OpenAPI document (development only) | `Program.cs` |
| Application-assigned GUID keys, soft delete, and the append-only audit trail | `EntityBase`, `AuditSaveChangesInterceptor`, `AuditLog` |

**Not yet built.** These are the real gaps:

| Area | Target (this document) | Code today | Closes in |
|---|---|---|---|
| Database engine | **Azure SQL Server** | **done** — `Microsoft.EntityFrameworkCore.SqlServer`; migrations and snapshot regenerated on SQL Server | feature 01 |
| API hosting | ACA from a container image | **done** — `src/api/Dockerfile` builds the image, `.dockerignore` keeps local build output out of the context. No `docker-compose.yml`: neither target needs a local multi-service stack | feature 01 |
| Web hosting | Azure Static Web Apps by GitHub workflow | not built | feature 10 |
| Test harness | xUnit per layer, container-backed tiers ([testing-and-tdd.md](testing-and-tdd.md)) | **done** — the three `*.Test` projects reference the layer each exercises; a run with the containers up passes everything, and a run without them skips the container-backed tiers rather than failing. `TrailBlaze.Service.Test` is no longer empty — the caller-role tests live there ([25-service-test-tier-is-empty.md](tech-debt/25-service-test-tier-is-empty.md)). The counts are in [testing-and-tdd.md](testing-and-tdd.md) and nowhere else | feature 01 |
| Blob abstraction | `IStorageRepository`, three containers, no fake | **done** — `IStorageRepository` in `TrailBlaze.Interface` and the single Azure adapter in `TrailBlaze.Repository`. The storage tier runs that adapter against Azurite, so nothing stands in for it | feature 01 |
| Activity and media tables | the data model below | only `users` and the audit table exist | feature 04 |
| Profile columns | `users` carries `Description`, `AvatarBlobPath`, `PreferredTheme`, `PreferredLanguage`, `Email` | **done** — all five exist, bounded to the lengths in the data model. The narrowing `ALTER COLUMN` lives in `AddUserProfileColumns`, which is applied to the local development database (2026-09-19) and to no Azure SQL Database, because no deployed environment exists yet ([feature 11](features/11-e2e-verification.md)). What it does to a populated table is asserted in `MigrationNarrowingTests`, not assumed | feature 02 |
| Profile API | `PUT /user/me`, `POST`/`DELETE /user/me/avatar` | **done** — all four profile routes exist and return DTOs; the behaviour behind them is implemented but **not yet covered by tests** (see [02-entra-auth.md](features/archive/02-entra-auth.md#testing-status)) | feature 02 |
| Administrator | one admin, set by hand; role read from the row, never from a claim | **done** — `ICallerRoleService` answers the caller's role from their own row, and `users.Role` is not null, defaults to `User`, and is check-constrained to the closed set. The admin is one row whose `Role` column reads `Admin`, updated directly in the database: **nothing in the application seeds, promotes or writes it** — see [03-admin-seeding.md](features/03-admin-seeding.md#decisions) for why the startup seeder this row originally described was removed | feature 03 |
| Frontend integration | the Figma export wired to the API (feature 10) | the export is committed but is **entirely mock data** — no API call, no MSAL, the role hard-coded to `user` and upload controls inert. It is design intent, not a working client | feature 10 |

**The key shape is settled: `users.Id` is the Entra object id.** This document originally proposed
a surrogate `Id` plus a separate unique `EntraObjectId`, while the code used the object id **as**
the primary key. That divergence is closed in the code's favour (2026-09-16), and the data model
below now describes the code rather than proposing an alternative. The `oid` is immutable in Entra
and unique within a tenant, so it needs no second column and no separate unique index to be one
row per person. What the surrogate shape would have bought is room for a `users` row that exists
before its owner ever signs in — and there is no invitation, import, or admin-created row in this
product, so that room has nothing to hold. Adding one later is the moment to reopen this.

That shape has a security consequence worth stating where the keys are. The `users` primary key
**is** the Entra object id, so any response carrying a user id hands the recipient a durable Entra
identifier. Decision #30 therefore forbids the anonymous payload from carrying one: anonymous
responses name the creator by display name only, and the id travels only on authenticated
responses. That rule is not a consequence of this key shape to be relaxed if the shape ever
changes — the anonymity of a payload should not depend on which shape the primary key happens to
take.

## Frontend build

UI screens are authored in **Figma Make**, which exports a React project used from `src/web/`.
The only hand-written frontend work is **integrating the exported app with the backend API** —
MSAL auth wiring, API calls, wiring responses into the exported components, and role gating.
Per [testing-and-tdd.md](testing-and-tdd.md), the frontend is **out of TDD scope**; consequently
every feature below specs the **backend/API slice** the exported UI calls, not screens or widgets.

## Decisions log

Every requirement decision from the grilling session, in order:

| # | Decision | Resolution |
|---|---|---|
| 1 | What the product is | An **activity journal**: a user browses activities ordered by date descending and attaches images/videos to each |
| 2 | Audience for activity text vs media | **Activity text is public or restricted by the entry's visibility** (see #26); **images and videos always require sign-in** — this half is unchanged and applies at every visibility level |
| 3 | Is it one journal or many | **One shared journal** — everyone posts to one feed; visibility governs who may read an entry, ownership governs who may edit it. Private entries are an escape hatch within the shared feed, not private journals (see #26) |
| 4 | Media storage | **Azure Blob Storage** (not local disk, not the database) |
| 5 | Blob endpoint per environment | **Real Azure Storage account for the application in every environment**, dev included. **Scoped 2026-09-18:** the *test* tier now runs against containers from `docker-compose.test.yml` — Azurite for storage, SQL Edge for the database — so "dev and tests included" no longer holds for tests. Decision #6 is why |
| 6 | Tests vs. the live Azure dependency | **No fake for storage or for the database.** The tier runs the real implementations against those containers by default, and skips when they are not running. **Reversed 2026-09-18** (was: an in-memory `IStorageRepository` fake in unit tests plus a credentialed integration tier). The fake implemented the contract it was asserting, so it proved that a dictionary tolerates a key — and a fake shadows the real implementation's invariants while appearing to test them |
| 7 | How media reaches the browser | **Short-lived SAS URLs** issued by an authenticated endpoint; container stays private |
| 8 | Authentication | **Entra ID** |
| 9 | Roles | **User + Admin**; admin can edit/delete any activity |
| 10 | Which date drives the sort | **User-chosen activity date**; backdating allowed; a `CreatedOn` audit timestamp is kept as the tiebreaker |
| 11 | Activity fields | `Date`, `Location`, `Title`, `Description` (optional), `CoverImage` (optional), `Type` (visibility, see #26) |
| 12 | Location capture | **Free-text place name** — no lookup, no structured fields |
| 13 | Cover image audience | **Follows the activity's visibility** — public for a Public activity, SAS-only for Shared and Private ones (see #29). Supersedes the earlier "always public", which held only while every activity was public |
| 14 | Where the cover comes from | **Its own upload** into a container chosen by the activity's visibility; never picked from, derived from, or re-pointed at private media (see #29) |
| 15 | Video handling | **Store as-is**; validate content type and size; no transcoding, no thumbnails |
| 16 | Backend & data stack | **.NET 10 layered + Azure SQL Server** (swapped from the inherited PostgreSQL in feature 01; the swap is done) |
| 17 | Where the UI comes from | **Figma Make export**, as in the source project; frontend not test-first |
| 18 | Feature ladder | **Written fresh for TrailBlaze** — the inherited ladder described a dynamic-form platform |
| 19 | Repository | **New GitHub repository** (`github.com/RhaegarC`), `develop` integration / `master` production |
| 20 | Scaffolding adaptation | **Full adaptation** of `.claude/` plus `PRD.md`, `testing-and-tdd.md`, `features/` as foundation work |
| 21 | Admin surface | **Elevated rights only** — no dedicated admin screens |
| 22 | Backend vs. frontend sequencing | **API-first**; Figma integration is its own late feature |
| 23 | Public list behaviour | **Paginated, date-descending, no search** |
| 24 | Upload limits | **~20 media per activity**, images ≤ 10 MB, videos ≤ 200 MB |
| 25 | Date representation | **Calendar date only** — no time, no timezone, no UTC-midnight conversion |
| 26 | Per-activity visibility | **`Type` ∈ {`Public`, `Shared`, `Private`}**, required, defaulting to `Public`. `Public` = anonymous may read; `Shared` = signed-in users only; `Private` = the owner only (and admins). Visibility gates **reading**; it never gates media, which needs sign-in at every level (#2). Added from the Figma export, 2026-09-15 — this supersedes the earlier flat "everything is public-read" model and reverses the earlier rejection of private entries in the shared feed, which was rejected on the assumption that private meant *separate journals* |
| 27 | Media is collaborative | **Any signed-in user who can see an activity may add media to it**, not only its owner. Each `media` row records its **uploader**, and the detail view groups items by uploader. Deletion is allowed to the **uploader, the activity's owner, or an admin**. The 20-per-activity cap (#24) is unchanged and counts the activity's items regardless of uploader |
| 28 | User profile & preferences | **Display name, bio, avatar, theme and language are stored server-side per user** and edited on a profile screen. Avatar lives in the **public** `avatars` container. Theme ∈ {`Dark`, `Light`}, language ∈ {`en`, `zh`}; both are presentation preferences and carry no authorization meaning |
| 29 | Cover container follows visibility | A cover is uploaded **directly into the container its activity's visibility requires**: `covers` (public) for a Public activity, `media` (private, SAS-served) for Shared and Private ones. Changing an activity's `Type` across that line **moves the cover** — see "Media storage & delivery". #14's rule that a cover is its own upload and is never derived from private media stands unchanged |
| 30 | Anonymous payload scope | The anonymous response may carry the activity's **media count** and its **creator's display name**. It may **not** carry a user id, a blob path, a SAS URL, or any per-item media field. Relaxes the stricter rule feature 05 originally stated, which forbade the count as media-derived |

## Data model

Every entity derives from **`EntityBase`**, so the shared columns below are present on all three
tables. They are drawn once, here, rather than repeated in the diagram:

| Column | Type | Notes |
|---|---|---|
| `Id` | string (GUID) | PK — **assigned by the application at construction**, not by the database |
| `CreatedBy` | string, nullable | caller identity at insert |
| `CreatedOn` | datetimeoffset | stamped by `AuditSaveChangesInterceptor` at insert, then not touched again — so it **is** stamped on every write path. There was a second, dead writer on the delete path; it was deleted ([item 01](tech-debt/archive/01-audit-columns-have-two-writers.md), archived) |
| `LastModifiedBy` | string, nullable | caller identity at the last update |
| `LastModifiedOn` | datetimeoffset | set on every update, but **not** at insert — a row that has never been updated carries `default(DateTimeOffset)`, i.e. the year 1: [item 20](tech-debt/20-lastmodified-unset-on-insert.md) |
| `IsDeleted` | bit, nullable | soft delete — a global query filter hides `true` rows by default |

The `users` row is the one exception to the `Id` rule: see the open key-shape decision below the
current-state table.

```mermaid
erDiagram
    User ||--o{ Activity : creates
    Activity ||--o{ Media : contains
    User ||--o{ Media : uploads
    User {
        string Id PK "GUID, app-assigned"
        string EntraObjectId UK
        string Email
        string DisplayName
        string Role
        string Description "nullable, the profile bio"
        string AvatarBlobPath "nullable, public avatars container"
        string PreferredTheme "Dark | Light"
        string PreferredLanguage "en | zh"
    }
    Activity {
        string Id PK "GUID, app-assigned"
        string Title
        string Location
        date ActivityDate
        string Description "nullable"
        string Type "Public | Shared | Private"
        string CoverImageBlobPath "nullable, container by Type"
        string CreatedByUserId FK
    }
    Media {
        string Id PK "GUID, app-assigned"
        string ActivityId FK
        string UploadedByUserId FK "who added this item"
        string Kind "Image | Video"
        string BlobPath "always the private container"
        string ContentType
        bigint SizeBytes
        string OriginalFileName
    }
```


| Table | Column | Type | Notes |
|---|---|---|---|
| `users` | `Id` | string | PK; **the Entra `oid` claim** — see "Current state vs. target" |
| | `Email` | nvarchar(320) | from the token |
| | `DisplayName` | nvarchar(200) | from the token, then edited on the profile screen |
| | `Role` | nvarchar(16) | **not null**, default `User`; `CK_Users_Role` admits only `User` \| `Admin` |
| | `Description` | nvarchar(500) | nullable; the profile **bio**, user-edited |
| | `AvatarBlobPath` | nvarchar(512) | nullable; **public** `avatars` container |
| | `PreferredTheme` | nvarchar(16) | `Dark` \| `Light`; default `Dark`; presentation only |
| | `PreferredLanguage` | nvarchar(16) | `en` \| `zh`; default `en`; presentation only |
| `activities` | `Id` | string (GUID) | PK, app-assigned |
| | `Title` | nvarchar(200) | required |
| | `Location` | nvarchar(200) | required, free text |
| | `ActivityDate` | date | required; **calendar date, no time**; drives the sort |
| | `Description` | nvarchar(max) | optional |
| | `Type` | nvarchar(16) | `Public` \| `Shared` \| `Private`; required, defaults to `Public`; **gates reads** |
| | `CoverImageBlobPath` | nvarchar(512) | optional; container follows `Type` — `covers` (public) for `Public`, `media` (private, SAS) for `Shared`/`Private` |
| | `CreatedByUserId` | string (GUID) | FK → `users.Id` |
| `media` | `Id` | string (GUID) | PK, app-assigned |
| | `ActivityId` | string (GUID) | FK → `activities.Id` |
| | `UploadedByUserId` | string (GUID) | FK → `users.Id`; **who added this item** — not necessarily the activity's creator |
| | `Kind` | nvarchar(16) | `Image` \| `Video` |
| | `BlobPath` | nvarchar(512) | **private** container; served only via SAS. Also holds covers of Shared/Private activities |
| | `ContentType` | nvarchar(128) | validated allowlist |
| | `SizeBytes` | bigint | validated ≤ 10 MB image / ≤ 200 MB video |
| | `OriginalFileName` | nvarchar(260) | display only |

Five consequences follow from the conventions above, and features below depend on them:

- **The sort tiebreaker is `CreatedOn`** (from `EntityBase`), not a column of its own. Ordering is
  `ActivityDate DESC, CreatedOn DESC` (Decision #10, feature 05).
- **Keys are strings the application assigns**, so an entity has its id before it is saved. This
  is what lets the audit trail record an `EntityId` on insert.
- **Deletes are soft.** `DELETE /api/activities/{id}` and `DELETE /api/media/{id}` mark rows
  `IsDeleted`; the global query filter removes them from every read path.
- **Visibility is a column, and it gates every read.** `activities.Type` is evaluated against the
  caller on the list *and* on the detail read. An activity the caller may not see is returned as
  **404, not 403** — a 403 would confirm the row exists, which is itself the fact being withheld.
  Mutations keep the 403 (see "Authentication & authorization").
- **Media belongs to an activity, not to a person.** `media.UploadedByUserId` exists to group and
  attribute items and to decide who may delete one; it never widens or narrows visibility, which
  is the activity's `Type` alone.

`AuditLog` is the one table that does **not** derive from `EntityBase`: it is append-only and
records `EntityId` as a plain string, because it must outlive the row it describes.

**Schema-change discipline.** A feature that changes the data model updates the table above in
the same PR. `docs/PRD.md#data-model` (this section) is the canonical reference.

## Authentication & authorization

Entra ID issues the bearer token; the API validates it and auto-provisions a `users` row on
first sight of a new `oid`. Role comes from the `Role` column, not from a token claim alone —
the platform issues the token, the database decides the privilege. The admin is one row with
`Role` set to `Admin`, written by hand against the deployed database — **there is no seeding
path, no admin configuration key, and no endpoint that grants the role**, so the only writer of
`Admin` is whoever operates the deployment. Elevated rights therefore exist without an
application that rewrites a privilege column on its own, at the cost of one guarantee the
database cannot give: a deployment with no administrator starts and looks healthy. That failure
is visible the first time an admin action is attempted rather than silent.

Authorization has two independent axes, and keeping them apart is what makes the matrix readable:
**visibility** (`activities.Type`) decides who may *read* an entry; **ownership**
(`activities.CreatedByUserId`) decides who may *mutate* it. An admin overrides both.

**Reading — what a caller receives, by the activity's `Type`:**

| `Type` | Anonymous | Signed-in non-owner | Owner | Admin |
|---|---|---|---|---|
| `Public` | text + cover + media count + creator display name | text + cover + media | full | full |
| `Shared` | **404** | text + cover + media | full | full |
| `Private` | **404** | **404** | full | full |

"Media count" and "creator display name" are the full extent of what an anonymous caller receives
beyond the text and cover (Decision #30): no user id, no blob path, no SAS URL, and no per-item
media field. The count and the name are what the export's list rows display; the ids behind them
are not.

`GET /api/activities` applies the same rule as a *filter* rather than a 404: an anonymous caller
pages the Public entries, a signed-in caller pages Public + Shared + their own Private, and an
admin pages everything.

**Mutating:**

| Operation | Anonymous | Signed-in non-owner | Owner | Admin |
|---|---|---|---|---|
| `POST /api/activities` | ❌ 401 | ✅ 201 | ✅ 201 | ✅ 201 |
| `PUT /api/activities/{id}` | ❌ 401 | ❌ 403 | ✅ 200 | ✅ 200 |
| `DELETE /api/activities/{id}` | ❌ 401 | ❌ 403 | ✅ 204 | ✅ 204 |
| `POST /api/activities/{id}/cover` | ❌ 401 | ❌ 403 | ✅ 200 | ✅ 200 |
| `POST /api/activities/{id}/media` | ❌ 401 | ✅ 201 if they can read the activity, ❌ **404** if not | ✅ 201 | ✅ 201 |
| `DELETE /api/media/{id}` | ❌ 401 | ✅ 204 **if they uploaded it**, ❌ 403 otherwise | ✅ 204 | ✅ 204 |
| `PUT /user/me`, `POST` / `DELETE /user/me/avatar` | ❌ 401 | ✅ self only — ❌ 403 for any other user | ✅ self only | ✅ self only |

Media mutation is the one place the two axes cross: **any signed-in user who can see an activity
may add media to it** (Decision #27), so the check is visibility, not ownership — except on a
Private activity they do not own, which they cannot see and therefore cannot contribute to.
`GET /api/activities/{id}/media` and `GET /api/media/{id}/url` return **401** anonymously and
**404** for a signed-in caller who cannot read the activity, so the media surface cannot be used
to probe for a Private entry's existence.

**Default deny.** Anonymous access is the exception, granted deliberately on exactly two read
endpoints. **404 and 403 are not interchangeable**: a caller who may not *see* an activity gets
404, because "this row exists" is itself the fact being withheld; a caller who can see it but may
not act on it gets 403. Visibility and ownership checks live in one service so the rule is
enforced in one place.

## Media storage & delivery

Three containers, each holding exactly one class of bytes, so that "is this blob public?" is
answerable from the container name alone:

- **`covers` (public)** — covers of **Public** activities. World-readable by design, so the
  anonymous list renders with images. Served as plain blob URLs.
- **`avatars` (public)** — user avatars. World-readable, plain blob URLs; an avatar is a
  deliberately public identity (Decision #28).
- **`media` (private)** — every activity's images and videos, **plus the covers of Shared and
  Private activities**. Never public. The browser obtains bytes only through a short-lived
  **SAS URL** minted by an authenticated endpoint.

**The cover follows its activity's visibility** (Decision #29). A cover is still **its own
upload** and is never derived from, picked from, or re-pointed at private media — that rule
(Decision #14) is unchanged and remains load-bearing. What changed is only *where the fresh
upload lands*, and it is a genuine fix rather than a refinement: while every activity was public,
a cover could safely go to a world-readable container. Once an activity can be Shared or Private,
a public cover URL discloses a picture from an entry the visitor cannot open.

**Changing `Type` across the public line moves the cover.** The blob's container is decided at
upload time, so an edit taking an activity from `Public` to `Shared`/`Private` must copy the
cover into `media`, update `CoverImageBlobPath`, and **delete the public blob**; the reverse move
copies it back to `covers`. This is required, not cosmetic: the old public URL is already
disclosed and may be cached or indexed, so leaving the bytes in place would keep a now-Private
entry's cover fetchable by anyone who ever saw the link. Both directions are asserted in
feature 08.

Because a SAS URL is a bearer token, the **expiry window is the real control**, not the URL's
secrecy. Unauthenticated requests to the SAS endpoint must fail with 401 before any blob
operation is attempted.

Validation on upload: content type against an allowlist, size against the caps in Decision #24,
and a count check against the activity's existing media — counted across **all** uploaders, since
media is collaborative (Decision #27).

## API surface

| Method | Route | Auth | Purpose |
|---|---|---|---|
| `GET` | `/api/activities?page=&pageSize=` | anonymous | Paged list, date descending. Anonymous callers receive **Public** entries only; a signed-in caller additionally receives **Shared** and their own **Private**. Each row is text, `Type`, media count, creator display name and a cover URL; **never** a user id, blob path or SAS URL for an anonymous caller (Decision #30) |
| `GET` | `/api/activities/{id}` | anonymous | The same payload for one entry. **404** for an entry the caller may not read |
| `POST` | `/api/activities` | user | Create — `Type` required, defaulting to `Public` |
| `PUT` | `/api/activities/{id}` | owner/admin | Update, including `Type` — crossing the public line **moves the cover** |
| `DELETE` | `/api/activities/{id}` | owner/admin | Delete (cascades media) |
| `POST` | `/api/activities/{id}/cover` | owner/admin | Upload/replace cover → `covers` (public) if the activity is `Public`, otherwise `media` (private, SAS) |
| `GET` | `/api/activities/{id}/media` | anyone who can read the activity | Media metadata, each item carrying its uploader |
| `POST` | `/api/activities/{id}/media` | any signed-in caller who can read the activity | Upload image/video → private container (collaborative, Decision #27) |
| `GET` | `/api/media/{id}/url` | anyone who can read the activity | Mint a short-lived SAS URL |
| `DELETE` | `/api/media/{id}` | uploader / activity owner / admin | Delete media (row + blob) |
| `GET` | `/user/me` | user | The caller's own row, inserted on first call — **already implemented** |
| `PUT` | `/user/me` | self | Update display name, bio, preferred theme and language |
| `POST` | `/user/me/avatar` | self | Upload/replace avatar → public `avatars` container |
| `DELETE` | `/user/me/avatar` | self | Remove the avatar (clears the field and deletes the blob) |
| `GET` | `/health` | anonymous | Liveness — **already implemented** |

`pageSize` is capped server-side (default 20, max 100) so the endpoint cannot be made to return
an unbounded result set.

**The cover URL in the two read rows is a SAS URL whenever the bytes are private.** A `Public`
activity's cover comes back as a plain public URL; a `Shared` or `Private` one comes back as a
short-lived SAS minted on the same terms as any other private blob (Decisions #13/#29) — the client
sees one field either way and never has to know which container holds the bytes. This makes the
list endpoint a **second SAS producer**, alongside `GET /api/media/{id}/url`, which is a deliberate
widening recorded in Decision #29's consequences: it means the list mints up to one SAS per
non-`Public` row per page. Both producers go through the same repository method, so the TTL cap and
the read-only scope stay single-sourced (feature 07).

The `/user/...` routes are stated here in lowercase for readability; the implemented controller
uses the ASP.NET `[controller]` token, which yields `/User/me`. That casing divergence is a known
inconsistency tracked as [item 04](tech-debt/04-usercontroller-route-convention.md), not a second
route — and its repair is a **breaking route change**, so it is sequenced before anything consumes
these paths (features 10 and 11 both do).

## Deployment

**No reverse proxy, no local multi-service stack.** The API is an image on **Azure Container
Apps**, built from `src/api/Dockerfile`; the web app is a static build on **Azure Static Web
Apps**, deployed by GitHub workflow. There is no `docker-compose.yml` — neither target consumes
one, and local development is `dotnet run` against the real cloud resources.

Everything the API talks to is a real cloud resource reached by configuration: **Azure SQL
Database** (which has no container image to run locally), **Azure Blob** and **Entra ID**. Secrets
therefore live in user-secrets locally and in pipeline variables for deployment.

**The test tier is the exception, and it is a different thing from a local stack.** `dotnet test`
starts `src/api/docker-compose.test.yml`, which brings up SQL Edge and Azurite purely so the
database and storage tiers have an engine to speak to. It starts no application process, serves no
request, and stands in for nothing the deployed product uses — it replaces the two *fakes* the
suite used to carry, not the two *cloud services* the product uses.

**Migrations are applied by the deployment pipeline, not at API startup.** Azure Container Apps runs
several replicas, and replicas migrating concurrently on startup race each other over the same DDL.
The workflow runs `dotnet ef database update` before the new revision takes traffic, so exactly one
writer touches the schema. Until those workflows exist, applying a migration is a manual step —
`DbConnection="…" dotnet ef database update` — because nothing else performs it.

## Out of scope / deferred

- **Search and filtering** on the list (Decision #23 — pagination only for v1)
- **Video transcoding and poster thumbnails** (Decision #15) — be aware that an iPhone's HEVC
  `.mov` will not play in Chrome or Firefox; it is stored faithfully and simply won't render
- **Per-media visibility** — visibility is set **per activity** (Decision #26), never per item, so
  there is no "make this one photo public" flag (Decision #14). Items inherit the activity's
  visibility; the *class*-level split now reads: public bytes are covers of Public activities and
  avatars, everything else is SAS-only
- **Comments, likes, follows, or any social layer**
- **Dedicated admin screens** (Decision #21)
- **Multi-tenancy** — one journal, everyone sees the same feed. Visibility (Decision #26)
  narrows who may read an entry; it does not partition the product into separate journals and
  adds no per-user feed
- **Offline support / native mobile apps**
- **The inherited dynamic-form features** — conditional fields, remote lookups, and form config
  with live preview have no analogue in TrailBlaze (Decision #18) and are dropped, not deferred
