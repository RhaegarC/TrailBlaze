# Mission 1 — TrailBlaze Activity Journal

Status: **Planning** — derived from [docs/PRD.md](../PRD.md) on 2026-09-15. The PRD stays the
living reference (decisions log, canonical data model, permission table).

## Goal

An activity journal that is public **by default rather than in principle**. Anyone browses the
`Public` activities by date descending with their covers; signing in widens that to `Shared` entries
and the caller's own `Private` ones, and reveals the images and videos each activity carries. One
shared feed, populated by any signed-in user; the activity's `Type` decides who may read it and
ownership decides who may edit or delete (Decisions #26/#27).

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
write anything. Feature **09** then imposes the visibility, ownership and admin rules on top. This
is a build-order choice, not an oversight: the mechanics stay independently testable, and 09 gets
to state the full permission matrix as its own acceptance criteria. The consequence is that
**04–08 are not safe to deploy** — and with per-activity visibility (Decision #26) that is a
sharper statement than it used to be, since until 09 lands a `Private` activity is not actually
private. `develop` should not be treated as a usable environment until 09 is merged. If that trade
is unwelcome, move 09 to run immediately after 04.

## Feature breakdown

Number = priority (lowest first = next to implement); file = `docs/features/NN-name.md`.

**This table is the only place a feature's status is written down.** The `Status` column owns
progress and the summary cell owns what the slice is; a feature's own file states its lifecycle and
nothing more, and the PRD's [current-state
table](../PRD.md#current-state-vs-target) states capability rather than progress.

| # | Feature (file) | Depends on | Summary — the backend/API slice | Status |
|---|---|---|---|---|
| 01 | [foundation](archive/01-foundation.md) | — | Layered `TrailBlaze.*` solution + sibling `*.Test` projects that **run tests**; Azure SQL Database via EF Core with migrations applied by the pipeline; `Dockerfile` for the ACA image; `IStorageRepository` abstraction; config for Azure Blob | archived — merged in PR #3. The in-memory storage fake it shipped was deleted on 2026-09-18 ([testing-and-tdd.md](../testing-and-tdd.md)) |
| 02 | [entra-auth](archive/02-entra-auth.md) | 01 | Backend validates Entra ID bearer tokens; users auto-provisioned on first sight of an `oid`; caller identity available to services; **self-service profile** — display name, bio, avatar, theme, language | archived — merged in PR #4, **tests deferred**, so unfinished: the profile slice is implemented and unproven ([item 12](../tech-debt/12-feature-02-tests-deferred.md)) |
| 03 | [the role column](archive/03-admin-seeding.md) | 02 | `Role` stored on `users`, defaulting to `User` and closed to `User`/`Admin`; one admin set by hand; role readable by the authorization path | archived — merged in PR #12, and unlike 02 its tests exist. **The server-side role read the authorization path needs does not exist yet** — it was built and removed in review as unconsumed, and [09](09-permission-enforcement.md) adds it |
| 04 | [activity-crud](04-activity-crud.md) | 02 | Create/read/update/delete an activity: title, location, activity date, optional description, and `Type` (visibility). Validation: title/location/date required; `ActivityDate` is a calendar date. **`Type` is stored here, enforced in 05/09** | in progress — `feature/04-activity-crud` |
| 05 | [public-activity-list](05-public-activity-list.md) | 04 | The read surface — paged, date descending, `pageSize` clamped. **Visibility-scoped**: anonymous sees `Public` only; a signed-in caller adds `Shared` and their own `Private`; an unreadable entry is a 404 | not started |
| 06 | [media-upload](06-media-upload.md) | 04 | Upload images/videos to the **private** container: content-type allowlist, size caps (10 MB / 200 MB), ≤ 20 per activity; list media metadata. **Collaborative** — any signed-in caller who can read the activity may contribute; each item records its **uploader** | not started |
| 07 | [sas-delivery](07-sas-delivery.md) | 06 | `GET /api/media/{id}/url` mints a **short-lived SAS URL**, and **only** for an authenticated caller — rejected before any blob operation otherwise | not started |
| 08 | [cover-images](08-cover-images.md) | 04 | Cover is a **separate upload** whose container **follows the activity's `Type`** — public `covers` for `Public`, private `media` otherwise; a `Type` change across that line **moves** the cover. Never derived from private media | not started |
| 09 | [permission-enforcement](09-permission-enforcement.md) | 03, 04 | **Two axes enforced in one service**: visibility gates reads, ownership gates mutations, `Admin` overrides both. Anonymous denied everywhere except the two public read endpoints. Media upload is the axis crossing — allowed to any caller who can read the activity | not started |
| 10 | [figma-integration](10-figma-integration.md) | 05, 07, 08, 09 | The Figma-exported app wired to the API: MSAL login, API calls, role gating, **visibility badges**, **media grouped by uploader**, and the **profile screen**. **Non-TDD** — verified manually | not started |
| 11 | [e2e-verification](11-e2e-verification.md) | 10 | Full-stack pass against a running stack: anonymous `Public`-only list → sign in for `Shared` → media visible → create with `Type` → collaborative upload by a second user → grouped by uploader → edit own → a `Private` entry 404s for a stranger → admin override | not started |

## Non-TDD tracks (not feature files)

- **Figma Make authoring** — screen design happens in Figma Make; the export is consumed by
  feature 10. Only the API integration is hand-written.
- **Deployment prerequisites** mostly fold into feature 01.

## Definition of Done (checked by `/sprint-status`)

- [x] Layered `TrailBlaze.*` backend solution builds; `dotnet test` green from `src/api/` **with a
      non-zero test count** — a green run over zero discovered tests does not count, and was the
      state when this line was written. [testing-and-tdd.md](../testing-and-tdd.md) holds the
      current counts, and is the only document that does
- [ ] Entra auth: backend validates bearer tokens; users auto-provisioned; exactly one admin
      — open on the token half, whose tests were deferred ([item 12](../tech-debt/12-feature-02-tests-deferred.md))
- [ ] Self-service profile complete: the caller can edit display name, bio, theme and language and
      upload an avatar, on their own row only, with `Role` not writable through the profile route
- [ ] Activity CRUD complete with validation (title, location, calendar-date `ActivityDate`) and
      `Type` required, defaulting to `Public`
- [ ] Anonymous visitors can page the activity list, date descending; `pageSize` clamped; the list
      is **visibility-scoped**, not merely unauthenticated
- [ ] Images and videos upload to the private container within the validation caps, to **any
      signed-in caller who can read the activity**, each item recording its uploader
- [ ] Media is reachable only via short-lived SAS URLs minted for authenticated callers
- [ ] Cover images upload separately, into the container the activity's `Type` requires, and render
      in the list; a `Type` change across the public line **moves** the cover
- [ ] Visibility, ownership and admin rules enforced in one service; anonymous denied except the two
      public reads; a `Private` entry is **404** to a caller who may not read it, **403** to one who
      may read but not act
- [ ] Figma-exported app integrated with the API (MSAL, role gating, media grouped by uploader,
      visibility badges, the profile screen); the export's mock data replaced by real calls; E2E
      verified
- [ ] Each backend feature merged to `develop` with its tests (RED → GREEN)

## Open items

- **Figma export defects, not export timing.** The export exists in `src/web/`, so feature 10 is no
  longer blocked on its *arrival* — it is blocked on the export being **fixed in Figma Make and
  re-exported**. [Item 15](../tech-debt/15-web-app-cannot-call-the-api.md) owns that defect list,
  including which entries are re-export fixes rather than doc gaps; this file does not restate them.
- **Azure credentials in CI** — **re-stamped 2026-09-18, and the gap is narrower than this said.**
  The storage tier no longer needs credentials at all: it runs the real `AzureBlobStorageRepository`
  against the Azurite container, which carries no secret, so CI proves the blob implementation
  without an Azure account. What still needs real credentials is the much smaller set of claims only
  a real account can settle — its certificate, its ACL behaviour, and its API-version acceptance —
  plus feature 11's end-to-end tier.
  [Item 11](../tech-debt/11-no-ci-pipeline.md) owns the pipeline decision and
  [item 12](../tech-debt/12-feature-02-tests-deferred.md) the three assertions that need it.
- **Known divergences live in the debt register.** [docs/tech-debt/00-debt-log.md](../tech-debt/00-debt-log.md)
  is the single list of what the code does not yet do as the standard says. Nothing in that category
  is filed here or in a feature file any more — a claim in two places is a claim that will disagree
  with itself.
