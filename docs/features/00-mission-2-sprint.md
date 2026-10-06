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

*(2026-10-05 — **15 was implemented before 14**, so the two rows are out of priority order and the
rule above is broken once, deliberately. 14's criterion "the media listing signs the derivative"
presumes a listing that signs anything, and the listing did not: it carried metadata and the app
fetched one URL per item. Building the derivative first would have meant writing the serving path
twice — once over the fan-out and again over the listing 15 introduces — so 15 went first and 14 is
now the switch of which path the listing signs. 14's `Depends on` column records the dependency. No
other row moves.)*

## Feature breakdown

Number = priority (lowest first = next to implement); file = `docs/features/NN-name.md`.

**This table is the only place a feature's status is written down.** The `Status` column owns
progress and the summary cell owns what the slice is; a feature's own file states its lifecycle and
nothing more, and the PRD's [current-state
table](../PRD.md#current-state-vs-target) states capability rather than progress. Mission 1's table
keeps rows 01–12 and is the record of that mission.

| # | Feature (file) | Depends on | Summary — the backend/API slice | Status |
|---|---|---|---|---|
| 13 | [upload-size-limits](archive/13-upload-size-limits.md) | 06 | The image and video caps stop being compile-time constants and become **configuration**, with raised defaults (25 MB image / 200 MB video) and **absolute ceilings** (100 MB / 512 MB) that still refuse a runaway upload. The route-level request limits follow the ceilings, so a file the service would accept is never answered with a bare 413 | archived — merged in PR #50. Both caps come from configuration and clamp to their ceilings, the refusal names the cap in force, and the cover and avatar routes now carry the request-size limits they did not need while the image cap sat below Kestrel's 30 MB default. **One criterion is not claimed**: that an oversize request is answered with the service's 400 rather than a bare 413, on all three upload routes, is `verification-only` — every route is `[Authorize]` and the Api tier holds no token, so the attribute is unreachable from a test, and the manual pass has not been run |
| 14 | [image-thumbnails](14-image-thumbnails.md) | 06, 07, **15** | **Every uploaded image is stored twice** — the original plus a re-encoded derivative at a configured quality and maximum dimension — and the derivative is what the browser receives. Video is untouched: no transcoding, no poster frame, original streamed as today. Needs a new nullable column, so the PRD data model moves with it. **Implemented after 15, not before it** — see the numbering note above | **Not started** |
| 15 | [media-url-cache](archive/15-media-url-cache.md) | 07, 12 | The media listing **carries each item's read URL and expiry**, collapsing the app's 1+N fan-out into one request; blobs gain a `Cache-Control`; and the SAS expiry is aligned to a window boundary so successive listings mint the *same* URL and a browser cache can hit it. No route is added or removed. Read with 16: minting per item is cheap only while signing stays local archived — merged in PR #52. The listing mints a URL per item from one page expiry, the minting instant is rounded to a boundary taken from the cache window so successive listings produce the same string for as long as a browser may reuse the bytes, media uploads carry a bounded `private` `Cache-Control`, and the app asks once and holds what it was given. **Two criteria about the browser are not claimed**: no runner exists in `src/web`, so the reuse and the refresh are `verification-only` and the manual pass has not been run |
| 16 | [user-delegation-sas](16-user-delegation-sas.md) | 07, 12, 15 | **Not performance work.** The storage credential stops being the account key: the API signs read URLs with a **user delegation key** obtained as its own Entra identity, so no account key sits in configuration and the signatures become revocable. The delegation key is cached per key lifetime — the one backend cache this mission actually needs, since it is *not* caller-dependent. **Its first step is to settle whether the emulator can mint one at all**, because if it cannot, no tier exercises the delegation path | **Not started** |

## Definition of Done

- [ ] The image and video caps are read from configuration; absent gives the default, above the
      ceiling clamps to it; the refusal is a documented 400, not a 413
- [ ] An uploaded image is stored with a derivative and the browser is served the derivative, while
      the original stays retrievable
- [ ] A decode that cannot produce a derivative leaves the item stored and served from its original
- [ ] Video behaviour is unchanged — stored as-is, no derivative, served as today
- [x] A media listing carries a read URL and an expiry per item, and the app makes one request for a
      listing rather than one plus N (the app's half is `verification-only` —
      [15](archive/15-media-url-cache.md) records what was and was not established)
- [x] Media bytes carry a bounded, `private` cache header, and the SAS expiry remains the control
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
| `TrailBlaze.Service.Test` | 293 / 0 / 293 | 293 / 0 / 293 |
| `TrailBlaze.Repository.Test` | 66 / 61 / 127 | 127 / 0 / 127 |
| `TrailBlaze.Api.Test` | 20 / 0 / 20 | 20 / 0 / 20 |
| **All three** | **379 / 61 / 440** | **440 / 0 / 440** |

Bare-machine numbers are the honest description of a machine with nothing configured, not a failure:
the container tiers skip, and `Category=Container` is the only trait in the solution. *(2026-10-05 —
[13](archive/13-upload-size-limits.md) added nine test cases, all in the offline tiers, so the
bare-machine count and the container count moved by the same nine and the container column is
otherwise the Mission 1 close figure. The Repository row's bare split was taken from a
`Category!=Container` run and the container half from a `Category=Container` run, which partition the
same 125.)*

*(2026-10-06 — [15](archive/15-media-url-cache.md) added fourteen test cases: twelve in the service tier's
offline tests, all in `TrailBlaze.Service.Test`, and two in the storage tier, which is
container-tagged. So the container column moved by fourteen and the bare column by twelve, and the
two are no longer the same distance apart as they were. **The Repository row's bare split moved for a
second reason, and it is worth stating rather than leaving as a puzzle**: the row above was taken
with the emulator down, and this one with it up. The storage tier is container-tagged but skips only
when *nothing answers at its endpoint*, so a bare run on a machine with Azurite running executes its
19 storage tests and skips only the 61 that need SQL — 47 offline + 19 storage = the 66 passed, 61
skipped. `Category!=Container` is therefore not the offline run, exactly as
[testing-and-tdd.md](../testing-and-tdd.md) warns: it reports 47 / 0 / 47 here, and `Category=Container`
reports 19 / 61 / 80. The two partitions are of the same 127.)

*(2026-10-06 — the twelfth of those cases is a correction to 15 rather than an addition to it, and it
is recorded because the defect it caught was in the design rather than the code. The minting boundary
was a minute; the cache window it exists to serve is five. Since a browser keys its copy by the URL, a
URL that changed every minute missed a cache that was still valid for four more — so a returning
visitor re-downloaded, which is the case the feature was written for. The boundary is now taken from
the cache window rather than chosen beside it, and the client's refresh margin moved with it. The
count moved by one because the coupling is asserted rather than commented.)*

## Open items

- **The web half of 14 and 15 cannot be asserted.** `src/web` has no test runner, which bugs 01–03
  each record, so the client-side claims are `verification-only`: what was run is written down and the
  rendered result is a browser observation. *(2026-10-06 — [15](archive/15-media-url-cache.md) merged
  with its pass still unrun, and that pass was not decoration: the browser half is where the caching
  defect lived. The minting boundary was a minute against a five-minute cache window, so a returning
  visitor re-downloaded — invisible to every backend test, and found by reading the two numbers
  against each other rather than by running anything. The pass is still outstanding.)*
- **A merge that does not exist.** On 2026-10-05 the message "pr merged" arrived naming no pull
  request, and no merge matches it — the newest PR of any state is #48, and neither `origin/develop`
  nor `origin/master` had moved since 2026-09-30 11:01Z when it was checked. Recorded here so it is
  not lost; it is unrelated to this mission and blocks nothing. *(2026-10-05 — two merges have landed
  since, #49 and #50, and both named their number; neither is the message above, so the observation
  stands as written rather than as an open question.)*
- **Mission 1 closed on 2026-10-05, with one item unfinished.** [Feature 11](archive/11-e2e-verification.md)'s
  walkthrough ran — it is the only place the Admin override is exercised end to end, and the pass that
  observes [bug 03](../bugs/00-bug-log.md)'s fix, since the media and list reads widen for a signed-in
  caller. Feature 02's deferred token tests remain deferred in [02](archive/02-entra-auth.md). Neither
  is a performance concern and neither is carried here.
