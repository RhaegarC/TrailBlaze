# 04 — `UserController` violates the route convention, and its `index` action is scaffolding

Status: **open** · Kind: correctness · Impact: friction · Area: Api
Source: STANDARD §12.4 · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

Three divergences from the routing convention in STANDARD §2, all inherited from the scaffold and
all still present in [UserController.cs](../../src/api/TrailBlaze.Api/Controllers/UserController.cs):

| Line | Divergence |
|---|---|
| [28](../../src/api/TrailBlaze.Api/Controllers/UserController.cs#L28) | `[Route("[controller]")]` — yields `/User/me`, not `api/[controller]` |
| [29](../../src/api/TrailBlaze.Api/Controllers/UserController.cs#L29) | inherits `Controller`, not `ControllerBase` |
| [33-37](../../src/api/TrailBlaze.Api/Controllers/UserController.cs#L33-L37) | `Index()` returns `Ok("Welcome")` — a placeholder action with no caller and no meaning |

## Why it matters

**The route casing is a documented lie.** The PRD's API surface table, its permission matrix, and
every feature file that describes the profile endpoints say `/user/me`
([PRD.md:359-362](../PRD.md#L359-L362), [:294](../PRD.md#L294)). The application serves `/User/me`.
Anyone wiring a client from the PRD gets a 404, and the PRD is the document the whole project treats
as canonical. PRD:378-379 acknowledges the mismatch in prose; acknowledging it is not the same as it
being true.

This is `friction` rather than `silent-wrong` only because a 404 is loud. But it is *loud at the
wrong time* — during feature 10's client integration, where a case mismatch reads as an auth or CORS
problem and costs an afternoon.

**Inheriting `Controller`** is the lesser half: `Controller` adds view support the API does not use,
and it pulls MVC view services into the request path. It works; it just declares the wrong thing.

**`Index()` is worse than dead** — it is a route that exists, is authenticated, returns a bare
string, and means nothing. It will be found by whoever enumerates the API surface.

## Evidence

Checked against the code 2026-09-17.

- The three attributes and the placeholder action are as quoted above.
- The surrounding actions (`Me`, `UpdateMe`, `SetAvatar`, `RemoveAvatar`, lines 42–111) are real and
  wired, so this is scaffolding left around working code, not a stub controller.
- The PRD states `/user/me` at lines 71, 86, 294 and 359–362, and notes the divergence at 378–379.

## Testability

**testable** at the API tier: an integration test asserting the documented route resolves is
exactly the assertion the PRD implies and nothing currently makes. `GET /user/me` (documented) and
`GET /User/me` (served) cannot both be right, and the test picks one.

Note the test must assert on the **documented** path, so it fails today and passes after the fix.
A test asserting the current path would only pin the bug in place.

## Repair plan

**This is a breaking public route change and cannot be merged as an isolated debt fix.** Sequence it
deliberately:

1. Decide the convention's winner. `api/[controller]` per STANDARD §2 gives `/api/User/me`, which is
   *still* not what the PRD says. So there are two decisions here, not one: adopt the convention,
   and adopt a casing rule. Lowercase `/api/user/me` (a route token transformation) is the shape the
   PRD assumes; confirm before writing the test.
2. Update `PRD.md:359-362`, `:294`, `:86`, `:71` and `:378-379` in the same PR. The divergence note
   at 378–379 is deleted, not rewritten — there is nothing left to diverge.
3. RED: the documented path returns 200; the placeholder `index` route is gone (404 or absent).
4. GREEN: `[Route("api/[controller]")]`, inherit `ControllerBase`, delete `Index()`.
5. **Sequence before features 10 and 11.** Feature 11's acceptance criteria assert against these
   routes and feature 10 wires the client to them; both are cheaper against a settled path.

## Out of scope / related

- **Feature 11 asserts these routes** ([11-e2e-verification.md](../features/11-e2e-verification.md)),
  so this item and that feature touch the same strings. Whoever picks this up should check whether
  11 has started; if it has, coordinate rather than race.
- **The controller is the only one in the application.** The `index` action was the scaffold's
  sample; there is no other place this pattern could have been copied from, which is why it has
  survived.
- **`.http` file** (item [07](07-http-file-requests-weatherforecast.md)) is the same class of
  scaffold leftover in the same project — a request line for an endpoint that does not exist.

## Close checklist

- [ ] The casing decision recorded (convention name vs. PRD's lowercase) before the test is written
- [ ] A test asserting the documented path, confirmed red first
- [ ] `Index()` deleted; no route answers for it
- [ ] PRD's API surface, permission matrix and divergence note updated in the same PR
- [ ] Feature 10/11 not already in flight against the old path, or coordinated with
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
