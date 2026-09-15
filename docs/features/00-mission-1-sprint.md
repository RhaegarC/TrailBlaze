# Mission 1 — TrailBlaze Activity Journal

Status: **Planning** — derived from [docs/PRD.md](../PRD.md) on 2026-09-15. The PRD stays the
living reference (decisions log, canonical data model, permission table).

## Goal

A public-read activity journal. Anyone browses activities by date descending with their covers;
signing in reveals the images and videos each activity carries. One shared feed, populated by
any signed-in user; ownership decides who may edit or delete.

## How to read these features (working model)

- **Backend** is implemented test-first (RED → GREEN → refactor; tiers in
  [docs/testing-and-tdd.md](../testing-and-tdd.md)). It lives in `src/api/` as a layered
  solution — `TrailBlaze.Model`, `TrailBlaze.Repository`, `TrailBlaze.Service`,
  `TrailBlaze.Interface`, `TrailBlaze.Api` — each layer with a sibling `*.Test` xUnit project.
  Test command: `dotnet test` (from `src/api/`).
- **Frontend** is authored in Figma Make and exported into `src/web/` as a **single role-gated
  React app**. It is not test-first. Consequently each feature below specs the **backend/API
  slice** the exported UI calls and its acceptance criteria — it does not spec screens, widgets,
  or renderer behavior.
- A feature that changes the data model updates the PRD data-model table in the same PR
  ([schema-change discipline](../PRD.md#data-model)).
- Status reflects the doc lifecycle (file created → in progress → archived after PR to `develop`).

### Sequencing note — authorization lands late, deliberately

Features **04–08** build the CRUD and media mechanics while every signed-in user is still free to
write anything. Feature **09** then imposes the ownership and admin rules on top. This is a
build-order choice, not an oversight: the mechanics stay independently testable, and 09 gets to
state the full permission matrix as its own acceptance criteria. The consequence is that
**04–08 are not safe to deploy**, and `develop` should not be treated as a usable environment
until 09 is merged. If that trade is unwelcome, move 09 to run immediately after 04.

### Where the code actually stands (2026-09-15)

The repository was initialised from a generic layered .NET scaffold, which landed parts of 01 and
02 early. Read the `Status` column above with that in mind:

- **01 — in progress.** The five layers, the solution file, `EntityBase`, the audit interceptor, the
  soft-delete filter, `/health` and the OpenAPI document all exist. What is missing is the
  substance: the three `*.Test` projects are **empty and reference no project under test**, so
  `dotnet test` builds green and discovers nothing; the provider is still PostgreSQL (`Npgsql`);
  there is no `IStorageService`, no `docker-compose`, and no CI.
- **02 — in progress.** Entra bearer validation, the caller abstraction, and auto-provisioning
  behind `GET /user/me` are implemented. Remaining: the key-shape decision in Open items,
  concurrency safety on first-sight provisioning, claim truncation, and the email question.
- **03–11 — not started.**

The full accounting is in the PRD's [Current state vs. target](../PRD.md#current-state-vs-target).

## Feature breakdown

Number = priority (lowest first = next to implement); file = `docs/features/NN-name.md`.

| # | Feature (file) | Depends on | Summary — the backend/API slice | Status |
|---|---|---|---|---|
| 01 | [foundation](01-foundation.md) | — | Layered `TrailBlaze.*` solution + sibling `*.Test` projects that **run tests**; Azure SQL Server via EF Core with migrations at startup; `docker-compose` (api + Azure SQL Server); `IStorageService` abstraction with a fake; config for Azure Blob | in progress |
| 02 | [entra-auth](02-entra-auth.md) | 01 | Backend validates Entra ID bearer tokens; users auto-provisioned on first sight of an `oid`; caller identity available to services | in progress |
| 03 | [admin-seeding](03-admin-seeding.md) | 02 | `Role` stored on `users`; exactly one admin seeded from configuration at startup; role readable by the authorization path | not started |
| 04 | [activity-crud](04-activity-crud.md) | 02 | Create/read/update/delete an activity: title, location, activity date, optional description. Validation: title/location/date required; `ActivityDate` is a calendar date | not started |
| 05 | [public-activity-list](05-public-activity-list.md) | 04 | **Anonymous** `GET /api/activities` — paged, date descending, `pageSize` clamped server-side; returns text + cover URL only | not started |
| 06 | [media-upload](06-media-upload.md) | 04 | Upload images/videos to the **private** container: content-type allowlist, size caps (10 MB / 200 MB), ≤ 20 per activity; list media metadata | not started |
| 07 | [sas-delivery](07-sas-delivery.md) | 06 | `GET /api/media/{id}/url` mints a **short-lived SAS URL**, and **only** for an authenticated caller — rejected before any blob operation otherwise | not started |
| 08 | [cover-images](08-cover-images.md) | 04 | Cover is a **separate upload** into the **public** container; its URL appears in list and detail responses; never derived from private media | not started |
| 09 | [permission-enforcement](09-permission-enforcement.md) | 03, 04 | Owner-only edit/delete; admin override; anonymous denied everywhere except the two public read endpoints. Enforced in one service | not started |
| 10 | [figma-integration](10-figma-integration.md) | 05, 07, 08, 09 | The Figma-exported app wired to the API: MSAL login, API calls, role gating, media rendering. **Non-TDD** — verified manually | not started |
| 11 | [e2e-verification](11-e2e-verification.md) | 10 | Full-stack pass against a running stack: anonymous list → sign in → media visible → create → upload → edit own → admin override | not started |

## Non-TDD tracks (not feature files)

- **Figma Make authoring** — screen design happens in Figma Make; the export is consumed by
  feature 10. Only the API integration is hand-written.
- **Deployment prerequisites** mostly fold into feature 01.

## Definition of Done (checked by `/sprint-status`)

- [ ] Layered `TrailBlaze.*` backend solution builds; `dotnet test` green from `src/api/` **with a
      non-zero test count** — a green run over zero discovered tests does not count, and is the
      state today
- [ ] Entra auth: backend validates bearer tokens; users auto-provisioned; exactly one admin seeded
- [ ] Activity CRUD complete with validation (title, location, calendar-date `ActivityDate`)
- [ ] Anonymous visitors can page the activity list, date descending; `pageSize` clamped
- [ ] Images and videos upload to the private container within the validation caps
- [ ] Media is reachable only via short-lived SAS URLs minted for authenticated callers
- [ ] Cover images upload separately to the public container and render in the public list
- [ ] Ownership and admin rules enforced in one service; anonymous denied except the two public reads
- [ ] Figma-exported app integrated with the API (MSAL, role gating, media rendering); E2E verified
- [ ] Each backend feature merged to `develop` with its tests (RED → GREEN)

## Open items

- **The `users` key shape — needs a decision, not an edit.** The PRD data model gives `users` a
  surrogate `Id` plus a unique `EntraObjectId`; the code instead uses the Entra object id **as**
  the primary key and has neither an `EntraObjectId` nor an `Email` column. Feature 02 and the
  PRD's `users` row cannot both be closed until one side moves.
- **Figma export timing** — feature 10 is blocked until the Figma Make export exists; everything
  before it is unblocked.
- **Azure credentials in CI** — the tagged storage integration tier needs them to run; without
  them CI proves the fake, not the blob implementation.
