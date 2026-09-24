# 09 — Permission Enforcement

Status: **In progress** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #2/#3/#9/#21/#26/#27 + "Authentication & authorization" + "API surface".

## Summary

The access-control core, and the most important feature in the ladder. It imposes the PRD's
full permission matrix on routes that features 04–08 built while every signed-in caller was
still free to write anything. Anonymous access is granted deliberately on exactly two read
endpoints and denied everywhere else; a signed-in user may mutate only their own activity, and
an admin may mutate any. Every visibility, ownership and role decision is evaluated in **one
service**, so the rule exists in one place rather than being re-derived per endpoint. After this
feature merges, `develop` stops being permissive-by-construction and becomes safe to deploy.

The matrix has **two independent axes**, and this feature owns both the enforcement and the
statement of them:

- **Visibility** (`activities.Type`) decides who may **read** an entry — `Public` for everyone,
  `Shared` for signed-in callers, `Private` for the owner and admins (Decision #26). Feature 05
  applies it to the two anonymous-reachable read routes; this feature owns it as the single
  predicate and applies it to **every** other route, so a `Private` activity is not reachable by
  a side door.
- **Ownership** (`activities.CreatedBy`) decides who may **mutate** an entry. Media is the
  exception that makes the two axes visibly cross: **any signed-in caller who can read an
  activity may add media to it** (Decision #27), so the media-upload check is visibility, not
  ownership — the thing this feature would most easily get wrong by assuming the older rule.

## Story

As the owner of this journal I want ownership and role rules enforced by a single authorization
service on every route so that anonymous visitors, ordinary users, and admins each get exactly
the access the PRD grants them — and nothing more by default.

## Dependencies

- [03-admin-seeding](archive/03-admin-seeding.md) (`users.Role` stored and constrained; the admin is a row
  someone sets by hand, so this feature must not assume one exists)
- [04-activity-crud](archive/04-activity-crud.md) (activities, their `CreatedBy`, and the routes this feature gates)

Features 05–08 (public list, media upload, SAS delivery, cover images) are **retrofitted** by this
feature: their endpoints ship permissive and are brought under the matrix here.

## Acceptance criteria

- [x] Visibility, ownership and role evaluation lives in **exactly one service** in
      `TrailBlaze.Service` (e.g. an `IActivityAuthorizationService`); no controller, endpoint
      filter, or repository performs its own visibility comparison, ownership comparison or role
      test — verified by there being a single place the rule can change. Feature 05's read filter
      consults the same service rather than restating the predicate. `IActivityAuthorizationService`
      is that service, registered inline in the composition root; `IActivityService` gave up
      `VisibleTo`/`CanRead`, and `OneRuleOneHomeTests` asserts neither comes back
- [x] `GET /api/activity` and `GET /api/activity/{id}` are the **only** endpoints in the API
      that anonymous callers may reach (Decisions #2/#13). The list returns 200 anonymously; the
      detail returns 200 anonymously **for a `Public` entry only**, and **404 for a `Shared` or
      `Private` one**. The list of two is read off the endpoint table by
      `AnonymousReachabilityTests`, which fails on a third grant and on an action carrying neither
      a grant nor an authorization attribute
- [x] **The visibility gate applies to every non-read route.** A `Private` activity is unreachable
      to a caller who is neither its owner nor an admin through `GET /api/activity/{id}/media`,
      `POST /api/activity/{id}/media` and `POST /api/activity/{id}/cover` — each returns **404**,
      not 403, so none of them can be used as a side door to confirm the entry exists. A `Shared`
      entry is reachable by any signed-in caller and not by an anonymous one
      *(2026-09-24 — `GET /api/media/{id}/url` left this list: the route does not exist, and
      building it is [07](07-sas-delivery.md)'s. The criterion moved there with it.)*
- [x] `GET /api/activity/{id}/media` returns **401 for anonymous** and, for a signed-in caller,
      follows the activity's visibility — 200 if readable, 404 if not.
      *(2026-09-24 — `GET /api/media/{id}/url` moved to [07](07-sas-delivery.md), which owns it.)*
- [x] `POST /api/activity` returns **401 for anonymous**, 201 for `User` and `Admin`
- [x] `PUT /api/activity/{id}` returns **401 anonymous / 200 owner / 403 authenticated
      non-owner / 200 `Admin`**
- [x] `DELETE /api/activity/{id}` returns **401 anonymous / 204 owner / 403 authenticated
      non-owner / 204 `Admin`**
- [x] `POST /api/activity/{id}/cover` returns **403 for an authenticated non-owner who can read
      the activity** and succeeds for the owner and for `Admin` — an authenticated non-owner
      cannot reach cover mutation
- [x] `POST /api/activity/{id}/media` is **not** owner-only: it succeeds for **any signed-in
      caller who can read the activity**, including a stranger on a `Public` or `Shared` entry
      (Decision #27). Encoding the older owner-only rule here is the single most likely way to
      regress this feature, so the criterion is stated as an allow rather than a denial
- [x] `DELETE /api/media/{id}` succeeds for the item's **uploader**, the **owner of its activity**,
      and an **admin**, and a fourth signed-in user gets **403**. `PUT /user/me`, `POST` and
      `DELETE /user/me/avatar` are **self-only** where `User` is concerned — a caller may never
      read or write another user's profile, and an attempt gets **403**. The self-only half is
      structural rather than a runtime check: no route on `UserController` takes a user id, so
      there is no parameter through which another profile could be named
- [x] A rejected mutation performs **no blob operation**: on a 403, a 401, or the visibility
      **404**, the request reaches neither `IStorageRepository` nor the repository — the check runs
      before any side effect *(the ordering claim, asserted by the purpose-built recording double
      described in the test plan below)*.
      *(2026-09-24 — the weaker "leaves no blob" half has no home and is not built. Driving the
      service against the real database and the real emulator would put a `MediaService` call in
      `TrailBlaze.Repository.Test`, the tier whose sibling project is `TrailBlaze.Repository`; the
      layer rule puts it in `TrailBlaze.Service.Test`, which has no container fixture and is a unit
      tier by construction. Named rather than quietly dropped, and the ordering double proves the
      stronger property anyway: a call that is never made leaves no blob.)*
- [x] Requests to gated routes carry no `Authorization` header → **401**, not 403; a valid token
      belonging to the wrong principal → **403**
- [x] **403 and 404 are not interchangeable, and the distinction is now load-bearing.** A caller
      who may not *see* an activity gets **404** on every route, because existence itself is
      withheld; a caller who *can* see it but may not act on it gets **403**. Both are asserted on
      the same route so the pair cannot silently collapse into one: `PUT` on another user's
      `Public` activity is **403**, and on another user's `Private` activity is **404**
- [x] **Default deny**: a route requires authentication unless an anonymous grant is explicitly
      enumerated for it — anonymous reachability is a deliberate list of two, never a fallthrough
      or a missing attribute
- [x] The admin determination is read from the **`users.Role` column**, not from a token claim
      alone (Decision #9) — a token carrying an admin-looking claim but backed by a `User` row is
      still denied admin override
- [x] `GET /health` remains anonymously reachable and returns 200
- [ ] After this feature merges, every route in the PRD "API surface" table matches its stated
      Auth column, and `develop` is safe to deploy (04–08 alone are not). *Open on two counts:
      the merge itself, and `GET /api/media/{id}/url`, which [07](07-sas-delivery.md) has still to
      build*

**Two claims above are weaker than they read, and both weakenings are deliberate.**

The weaker one is the "no blob" half of the rejected-mutation criterion, which has no tier to live
in and is not built. The other is the status codes. The 401/403/404 statuses are asserted at the tier that can decide them without a token pipeline —
the service outcome, the outcome-to-HTTP mapping, and the endpoint table. `TrailBlazeApiFactory`
boots against an unreachable connection string, so a request carrying a valid token would authenticate
and then fail on the connection rather than on the rule, which is a red for the wrong reason. **The
live-pipeline matrix is [feature 11](11-e2e-verification.md)'s**, and it is the same gap that file
already carries for 05–08; what lands here is every claim a token is not needed to make.

## Tests (TDD)

This is a **security hot spot** and must be test-first (RED → GREEN) per
[docs/testing-and-tdd.md](../testing-and-tdd.md).

- Unit (`TrailBlaze.Service.Test`) — **hot spot (security)**: drive the authorization service with
  an allow/deny **matrix over the PRD permission tables**: for each route,
  {anonymous, signed-in non-owner, owner, admin} × {activity exists, activity absent} × the
  activity's **`Type`** where the route reads it. The visibility axis is the new dimension — a
  three-value column multiplies the matrix, and the cells that matter most are the ones where a
  `Private` row must be indistinguishable from an absent one. Assert the anonymous grant is
  limited to the two read operations, that every other anonymous cell denies, and that a
  `User`-role caller carrying an admin-shaped claim is denied admin override.
- Unit (`TrailBlaze.Service.Test`) — **hot spot (the axis crossing)**: assert that
  `POST /api/activity/{id}/media` **allows** a non-owner who can read the activity and
  **denies** one who cannot, and that `DELETE /api/media/{id}` allows the uploader *and* the
  activity owner *and* an admin. These two are written as explicit allow-tests precisely because
  the intuitive-but-wrong owner-only rule would pass every other test in this file.
- Integration (`TrailBlaze.Api.Test`) — **moved to [feature 11](11-e2e-verification.md)**
  *(2026-09-24)*: exercise each endpoint over the real pipeline with a test token, asserting the
  exact status code — 401 unauthenticated, 403 authenticated-but-not-permitted, 404 absent **or
  invisible**, 200/201/204 allowed — and the **403/404 pair on the same route**. The factory boots
  against an unreachable connection string by design, so a token would carry a request through
  authentication and into a connection failure; the red would name the wrong thing. Test-token
  infrastructure arrives with the tier that can use it, rather than as scaffolding here.
  *(2026-09-18 — "leaves no blob" is weaker by one inference than the call count it replaced, and
  the weakening is worth stating: it cannot separate *never called storage* from *called storage,
  failed, and cleaned up*.)* The stronger ordering claim is kept where it belongs, with a
  **purpose-built recording double defined inside that one test**: it implements
  `IStorageRepository` only to record whether it was invoked, exists for no other test, and is not a
  stand-in for the repository. Authorization being evaluated before any blob operation is the single
  claim such a double is still sanctioned for. It is `NeverReachedStorage` in
  `ActivityServiceTests`, which records the member name it was reached through so a failure names
  the call rather than reporting a bare count.
- Integration (`TrailBlaze.Repository.Test`) — the `CreatedBy` lookup the ownership decision
  reads returns the right owner, including after an update by a *different* caller, and the
  visibility lookup reads the activity's current `Type`. *(changed 2026-09-18 — "this tier needs no
  database" no longer holds: a lookup that *returns* an owner is an executed query, and the
  repository tier now has a real engine to execute it against.)* `ToQueryString()` remains the offline instrument, at the model tier, where the claim is
  about the generated statement rather than about the row it selects
  ([testing-and-tdd.md](../testing-and-tdd.md)). It is `AuthorizationLookupTests`, and it asks the
  engine one column at a time rather than restating the rule in a second place.
- Regression guard: a test asserting the anonymous-allowed route set is exactly
  `GET /api/activity` and `GET /api/activity/{id}`, so a new endpoint added later without an
  explicit decision fails rather than silently defaulting open. `AnonymousReachabilityTests` also
  asserts the other half of default deny — that no controller action carries neither a grant nor an
  authorization attribute — which is the case the closed list cannot see.
- Regression guard (**moved to [feature 11](11-e2e-verification.md)**, 2026-09-24): a test
  asserting that no route other than those two returns a **200 to an anonymous caller** for a
  non-`Public` activity. This is the guard that catches a future endpoint which authenticates
  correctly but forgets the visibility predicate — the failure mode that leaving a `Private`
  entry's cover or media reachable would represent. It needs a running pipeline with seeded rows,
  which is what 11 stands up.

## Notes / non-goals

- No policy engine, no expression language, no permission table — the rules are the fixed matrix
  in the PRD, not data-driven grants.
- No dedicated admin screens (Decision #21): admin is elevated rights in the same routes and UI.
- **This feature does *not* introduce `activities.Type`; it enforces it.** The column and its
  round-tripping belong to feature [04](archive/04-activity-crud.md) and the read filter to
  [05](archive/05-public-activity-list.md). What lands here is the *single predicate* the other two
  consult, and its application to every remaining route. Stated explicitly because this feature
  is where a reader would expect visibility to be introduced, and looking for it here would make
  the 04 and 05 criteria look like they were missing something.
- Visibility remains **per activity, never per item** (Decision #14): there is no per-media flag,
  and no way to publish one photo out of a `Private` entry.
- No changes to the data model — ownership rides on `activities.CreatedBy` and visibility on
  `activities.Type`, both created by feature 04.
- This feature does not add authentication itself; it consumes the identity that 02 established.
  **It does have to add the role read.** 03 stored and constrained `users.Role`, and made it
  unreachable from a claim or a request body, but the server-side read of it was built and removed
  in review as unconsumed — `/user/me` answers the client's own question from the row, and nothing
  asked on the server's behalf. The criterion above, reading the admin determination from the column,
  is that read. It is this feature's first piece of work and it belongs in the authorization service
  the criterion at the top of this list calls for, not in a service of its own.
