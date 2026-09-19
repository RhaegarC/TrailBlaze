# 09 — Permission Enforcement

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
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
- **Ownership** (`activities.CreatedByUserId`) decides who may **mutate** an entry. Media is the
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
- [04-activity-crud](04-activity-crud.md) (activities, their `CreatedByUserId`, and the routes this feature gates)

Features 05–08 (public list, media upload, SAS delivery, cover images) are **retrofitted** by this
feature: their endpoints ship permissive and are brought under the matrix here.

## Acceptance criteria

- [ ] Visibility, ownership and role evaluation lives in **exactly one service** in
      `TrailBlaze.Service` (e.g. an `IActivityAuthorizationService`); no controller, endpoint
      filter, or repository performs its own visibility comparison, ownership comparison or role
      test — verified by there being a single place the rule can change. Feature 05's read filter
      consults the same service rather than restating the predicate
- [ ] `GET /api/activities` and `GET /api/activities/{id}` are the **only** endpoints in the API
      that anonymous callers may reach (Decisions #2/#13). The list returns 200 anonymously; the
      detail returns 200 anonymously **for a `Public` entry only**, and **404 for a `Shared` or
      `Private` one**
- [ ] **The visibility gate applies to every non-read route.** A `Private` activity is unreachable
      to a caller who is neither its owner nor an admin through `GET /api/activities/{id}/media`,
      `GET /api/media/{id}/url`, `POST /api/activities/{id}/media` and
      `POST /api/activities/{id}/cover` — each returns **404**, not 403, so none of them can be
      used as a side door to confirm the entry exists. A `Shared` entry is reachable by any
      signed-in caller and not by an anonymous one
- [ ] `GET /api/activities/{id}/media` and `GET /api/media/{id}/url` return **401 for anonymous**
      and, for a signed-in caller, follow the activity's visibility — 200 if readable, 404 if not
- [ ] `POST /api/activities` returns **401 for anonymous**, 201 for `User` and `Admin`
- [ ] `PUT /api/activities/{id}` returns **401 anonymous / 200 owner / 403 authenticated
      non-owner / 200 `Admin`**
- [ ] `DELETE /api/activities/{id}` returns **401 anonymous / 204 owner / 403 authenticated
      non-owner / 204 `Admin`**
- [ ] `POST /api/activities/{id}/cover` returns **403 for an authenticated non-owner who can read
      the activity** and succeeds for the owner and for `Admin` — an authenticated non-owner
      cannot reach cover mutation
- [ ] `POST /api/activities/{id}/media` is **not** owner-only: it succeeds for **any signed-in
      caller who can read the activity**, including a stranger on a `Public` or `Shared` entry
      (Decision #27). Encoding the older owner-only rule here is the single most likely way to
      regress this feature, so the criterion is stated as an allow rather than a denial
- [ ] `DELETE /api/media/{id}` succeeds for the item's **uploader**, the **owner of its activity**,
      and an **admin**, and a fourth signed-in user gets **403**. `PUT /user/me`, `POST` and
      `DELETE /user/me/avatar` are **self-only** where `User` is concerned — a caller may never
      read or write another user's profile, and an attempt gets **403**
- [ ] A rejected mutation performs **no blob operation**: on a 403, a 401, or the visibility
      **404**, the request reaches neither `IStorageRepository` nor the repository — the check runs
      before any side effect *(the `IStorageRepository` half is the ordering claim, and as of
      2026-09-18 it is assertable only by the purpose-built recording double described in the
      test plan below — the blanket fake it used to be counted against is deleted; the outcome half
      is "leaves no blob", asserted against a real backend)*
- [ ] Requests to gated routes carry no `Authorization` header → **401**, not 403; a valid token
      belonging to the wrong principal → **403**
- [ ] **403 and 404 are not interchangeable, and the distinction is now load-bearing.** A caller
      who may not *see* an activity gets **404** on every route, because existence itself is
      withheld; a caller who *can* see it but may not act on it gets **403**. Both are asserted on
      the same route so the pair cannot silently collapse into one: `PUT` on another user's
      `Public` activity is **403**, and on another user's `Private` activity is **404**
- [ ] **Default deny**: a route requires authentication unless an anonymous grant is explicitly
      enumerated for it — anonymous reachability is a deliberate list of two, never a fallthrough
      or a missing attribute
- [ ] The admin determination is read from the **`users.Role` column**, not from a token claim
      alone (Decision #9) — a token carrying an admin-looking claim but backed by a `User` row is
      still denied admin override
- [ ] `GET /health` remains anonymously reachable and returns 200
- [ ] After this feature merges, every route in the PRD "API surface" table matches its stated
      Auth column, and `develop` is safe to deploy (04–08 alone are not)

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
  `POST /api/activities/{id}/media` **allows** a non-owner who can read the activity and
  **denies** one who cannot, and that `DELETE /api/media/{id}` allows the uploader *and* the
  activity owner *and* an admin. These two are written as explicit allow-tests precisely because
  the intuitive-but-wrong owner-only rule would pass every other test in this file.
- Integration (`TrailBlaze.Api.Test`) — **hot spot (security)**: exercise each endpoint over the
  real pipeline with a test token, asserting the exact status code: 401 unauthenticated, 403
  authenticated-but-not-permitted, 404 absent **or invisible**, 200/201/204 allowed. Assert the
  **403/404 pair on the same route** — `PUT` on another user's `Public` activity is 403, on their
  `Private` activity is 404 — and that a denied request **leaves no blob**. **Changed 2026-09-18:
  that replacement is weaker by one inference, and the weakening is the point of saying it here.**
  The old assert counted calls into a fake `IStorageRepository`, which is deleted. "Leaves no blob"
  observes an outcome against a real backend — and it cannot separate *never called storage* from
  *called storage, failed, and cleaned up*, which the call count could. The stronger ordering claim
  is kept where it is wanted, with a **purpose-built recording double defined inside that one test**:
  it implements `IStorageRepository` only to record whether it was invoked, exists for no other test,
  and is not a stand-in for the repository. Authorization being evaluated before any blob operation
  is the single claim such a double is still sanctioned for; the outcome half is the container tier's.
- Integration (`TrailBlaze.Repository.Test`) — the `CreatedByUserId` lookup the ownership decision
  reads returns the right owner, including after an update, and the visibility lookup reads the
  activity's current `Type`. **Changed 2026-09-18: "this tier needs no database" no longer holds.**
  A lookup that *returns* an owner is an executed query, and the repository tier now has a real engine
  to execute it against — one database per test class, created with `Migrate()` and dropped on
  dispose. `ToQueryString()` remains the offline instrument, at the model tier, where the claim is
  about the generated statement rather than about the row it selects
  ([testing-and-tdd.md](../testing-and-tdd.md)).
- Regression guard: a test asserting the anonymous-allowed route set is exactly
  `GET /api/activities` and `GET /api/activities/{id}`, so a new endpoint added later without an
  explicit decision fails rather than silently defaulting open.
- Regression guard: a test asserting that no route other than those two returns a **200 to an
  anonymous caller** for a non-`Public` activity. This is the guard that catches a future endpoint
  which authenticates correctly but forgets the visibility predicate — the failure mode that
  leaving a `Private` entry's cover or media reachable would represent.

## Notes / non-goals

- No policy engine, no expression language, no permission table — the rules are the fixed matrix
  in the PRD, not data-driven grants.
- No dedicated admin screens (Decision #21): admin is elevated rights in the same routes and UI.
- **This feature does *not* introduce `activities.Type`; it enforces it.** The column and its
  round-tripping belong to feature [04](04-activity-crud.md) and the read filter to
  [05](05-public-activity-list.md). What lands here is the *single predicate* the other two
  consult, and its application to every remaining route. Stated explicitly because this feature
  is where a reader would expect visibility to be introduced, and looking for it here would make
  the 04 and 05 criteria look like they were missing something.
- Visibility remains **per activity, never per item** (Decision #14): there is no per-media flag,
  and no way to publish one photo out of a `Private` entry.
- No changes to the data model — ownership rides on `activities.CreatedByUserId` and visibility on
  `activities.Type`, both created by feature 04.
- This feature does not add authentication itself; it consumes the identity that 02 established.
  **It does have to add the role read.** 03 stored and constrained `users.Role`, and made it
  unreachable from a claim or a request body, but the server-side read of it was built and removed
  in review as unconsumed — `/user/me` answers the client's own question from the row, and nothing
  asked on the server's behalf. The criterion above, reading the admin determination from the column,
  is that read. It is this feature's first piece of work and it belongs in the authorization service
  the criterion at the top of this list calls for, not in a service of its own.
