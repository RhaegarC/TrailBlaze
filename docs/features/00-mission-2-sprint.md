# Mission 2 — Performance

Status: **Planning** — derived from [docs/PRD.md](../PRD.md) on 2026-10-05. The PRD stays the living
reference (decisions log, canonical data model, permission table).

## Goal

The journal works and is slow in three specific, observable ways, and this mission fixes exactly
those:

1. **The upload caps are numbers nobody chose.** They are compile-time constants, so an operator
   cannot raise them without a rebuild.
2. **An uploaded photo reaches the browser at full size** — a multi-megabyte file drawn into a grid
   tile a few hundred pixels wide.
3. **Every image is re-fetched, and every read URL re-minted.** A media listing does not carry its
   URLs, so the app asks for one per item, and nothing is cached on either side.

Nothing here changes what the product *is*. It changes how much of it crosses the network, and how
much memory the server spends to decide that.

**One row here is not performance work, and it says so.** [16](16-user-delegation-sas.md) moves the
storage credential from the account key to the application's own identity. It is in this mission
because it is the one change that would make feature 15's per-item signing cost a network call, so
the two are read together — but it is a security and configuration change, reviewed on its own
merits, and its acceptance criteria are about what the application holds rather than what it spends.

## How to read these features (working model)

- **Backend** is implemented test-first (RED → GREEN → refactor; tiers in
  [docs/testing-and-tdd.md](../testing-and-tdd.md)). It lives in `src/api/` as a layered solution —
  `TrailBlaze.Model`, `TrailBlaze.Repository`, `TrailBlaze.Service`, `TrailBlaze.Interface`,
  `TrailBlaze.Api` — each layer with a sibling `*.Test` xUnit project. Test command: `dotnet test`
  (from `src/api/`).
- **Frontend** is authored in Figma Make and exported into `src/web/`. It is not test-first, and its
  only hand-written part is the API integration — so each feature below specs the **backend/API slice**
  the exported UI calls and its acceptance criteria, and the client half is recorded as
  `verification-only` where it cannot be asserted.
- A feature that changes the data model updates the PRD data-model table in the same PR
  ([schema-change discipline](../PRD.md#data-model)).
- Status reflects the doc lifecycle (file created → in progress → archived after PR to `develop`).

Numbers continue from Mission 1 — features **13**, **14**, **15**, **16** — because its table fixes
"number = priority (lowest first = next to implement)" and a reader should not have to re-derive the
order. A number here is a position in this table, not a claim that the row is the same kind of work
as its neighbours; 16's row is the one that differs, and it is placed last so it is read after the
performance it interacts with.

## Feature breakdown

Number = priority (lowest first = next to implement); file = `docs/features/NN-name.md`.

**This table is the only place a feature's status is written down.** The `Status` column owns
progress and the summary cell owns what the slice is; a feature's own file states its lifecycle and
nothing more, and the PRD's [current-state
table](../PRD.md#current-state-vs-target) states capability rather than progress. Mission 1's table
keeps rows 01–12 and is the record of that mission.

| # | Feature (file) | Depends on | Summary — the backend/API slice | Status |
|---|---|---|---|---|
| 13 | [upload-size-limits](13-upload-size-limits.md) | 06 | The image and video caps stop being compile-time constants and become **configuration**, with raised defaults (25 MB image / 200 MB video) and **absolute ceilings** (100 MB / 512 MB) that still refuse a runaway upload. The route-level request limits follow the ceilings, so a file the service would accept is never answered with a bare 413 | **In progress** |
| 14 | [image-thumbnails](14-image-thumbnails.md) | 06, 07 | **Every uploaded image is stored twice** — the original plus a re-encoded derivative at a configured quality and maximum dimension — and the derivative is what the browser receives. Video is untouched: no transcoding, no poster frame, original streamed as today. Needs a new nullable column, so the PRD data model moves with it | **Not started** |
| 15 | [media-url-cache](15-media-url-cache.md) | 07, 12 | The media listing **carries each item's read URL and expiry**, collapsing the app's 1+N fan-out into one request; blobs gain a `Cache-Control`; and the SAS expiry is aligned to a window boundary so successive listings mint the *same* URL and a browser cache can hit it. No route is added or removed. Read with 16: minting per item is cheap only while signing stays local | **Not started** |
| 16 | [user-delegation-sas](16-user-delegation-sas.md) | 07, 12, 15 | **Not performance work.** The storage credential stops being the account key: the API signs read URLs with a **user delegation key** obtained as its own Entra identity, so no account key sits in configuration and the signatures become revocable. The delegation key is cached per key lifetime — the one backend cache this mission actually needs, since it is *not* caller-dependent. **Its first step is to settle whether the emulator can mint one at all**, because if it cannot, no tier exercises the delegation path | **Not started** |

## Definition of Done

- [ ] The image and video caps are read from configuration; absent gives the default, above the
      ceiling clamps to it; the refusal is a documented 400, not a 413
- [ ] An uploaded image is stored with a derivative and the browser is served the derivative, while
      the original stays retrievable
- [ ] A decode that cannot produce a derivative leaves the item stored and served from its original
- [ ] Video behaviour is unchanged — stored as-is, no derivative, served as today
- [ ] A media listing carries a read URL and an expiry per item, and the app makes one request for a
      listing rather than one plus N
- [ ] Media bytes carry a bounded, `private` cache header, and the SAS expiry remains the control
- [ ] The data-model change lands in the PRD in the same pull request as the migration
- [ ] No storage account key is read from configuration: the API signs read URLs as its own identity,
      and the required setting it replaces still stops startup when absent
- [ ] Each backend feature merged to `develop` with its tests (RED → GREEN)

## Current test counts

**This table is the only place the counts are written down**, and it is a measurement rather than a
derivation: each row is what a run printed, read off the summary as `Passed / Skipped / Total`. How
the tiers are shaped, and which one a new test belongs to, is
[testing-and-tdd.md](../testing-and-tdd.md)'s subject, not this file's. Mission 1's own table is the
frozen measurement taken when that mission closed.

| Project | Bare machine | With the containers |
|---|---|---|
| `TrailBlaze.Service.Test` | 273 / 0 / 273 | 273 / 0 / 273 |
| `TrailBlaze.Repository.Test` | 47 / 78 / 125 | 125 / 0 / 125 |
| `TrailBlaze.Api.Test` | 19 / 0 / 19 | 19 / 0 / 19 |
| **All three** | **339 / 78 / 417** | **417 / 0 / 417** |

Bare-machine numbers are the honest description of a machine with nothing configured, not a failure:
the container tiers skip, and `Category=Container` is the only trait in the solution. *(2026-10-05 —
carried forward from Mission 1's close: this sprint has added no test yet, and the figures above were
re-measured on a bare machine and unchanged. The container column follows the same test set, which is
why it moves with it.)*

## Open items

- **The web half of 14 and 15 cannot be asserted.** `src/web` has no test runner, which bugs 01–03
  each record, so the client-side claims are `verification-only`: what was run is written down and the
  rendered result is a browser observation.
- **A merge that does not exist.** On 2026-10-05 the message "pr merged" arrived naming no pull
  request, and no merge matches it — the newest PR of any state is #48, and neither `origin/develop`
  nor `origin/master` had moved since 2026-09-30 11:01Z when it was checked. Two undocumented local
  `release/*` branches exist. Recorded here so it is not lost; it is unrelated to this mission and
  blocks nothing.
- **Mission 1 closed on 2026-10-05, with one item unfinished.** [Feature 11](archive/11-e2e-verification.md)'s
  walkthrough ran — it is the only place the Admin override is exercised end to end, and the pass that
  observes [bug 03](../bugs/00-bug-log.md)'s fix, since the media and list reads widen for a signed-in
  caller. Feature 02's deferred token tests remain deferred in [02](archive/02-entra-auth.md). Neither
  is a performance concern and neither is carried here.
