# 09 — Permission Enforcement

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #2/#3/#9/#21 + "Authentication & authorization" + "API surface".

## Summary

The access-control core, and the most important feature in the ladder. It imposes the PRD's
full permission matrix on routes that features 04–08 built while every signed-in caller was
still free to write anything. Anonymous access is granted deliberately on exactly two read
endpoints and denied everywhere else; a signed-in user may mutate only their own activity, and
an admin may mutate any. Every ownership and role decision is evaluated in **one service**, so
the rule exists in one place rather than being re-derived per endpoint. After this feature
merges, `develop` stops being permissive-by-construction and becomes safe to deploy.

## Story

As the owner of this journal I want ownership and role rules enforced by a single authorization
service on every route so that anonymous visitors, ordinary users, and admins each get exactly
the access the PRD grants them — and nothing more by default.

## Dependencies

- [03-admin-seeding](03-admin-seeding.md) (`users.Role` stored and seeded; the admin identity exists)
- [04-activity-crud](04-activity-crud.md) (activities, their `CreatedByUserId`, and the routes this feature gates)

Features 05–08 (public list, media upload, SAS delivery, cover images) are **retrofitted** by this
feature: their endpoints ship permissive and are brought under the matrix here.

## Acceptance criteria

- [ ] Ownership and role evaluation lives in **exactly one service** in `TrailBlaze.Service`
      (e.g. an `IActivityAuthorizationService`); no controller, endpoint filter, or repository
      performs its own ownership comparison or role test — verified by there being a single
      place the rule can change
- [ ] `GET /api/activities` and `GET /api/activities/{id}` return **200 for anonymous**, `User`,
      and `Admin` — and these two routes are the **only** endpoints in the API that anonymous
      callers may reach (Decisions #2/#13)
- [ ] `GET /api/activities/{id}/media` and `GET /api/media/{id}/url` return **401 for anonymous**
      and 200 for `User` and `Admin`
- [ ] `POST /api/activities` returns **401 for anonymous**, 201 for `User` and `Admin`
- [ ] `PUT /api/activities/{id}` returns **401 anonymous / 200 owner / 403 authenticated
      non-owner / 200 `Admin`**
- [ ] `DELETE /api/activities/{id}` returns **401 anonymous / 204 owner / 403 authenticated
      non-owner / 204 `Admin`**
- [ ] `POST /api/activities/{id}/cover`, `POST /api/activities/{id}/media`, and
      `DELETE /api/media/{id}` all return **403 for an authenticated non-owner** and **succeed for
      the owner and for `Admin`** — an authenticated non-owner cannot reach cover mutation or
      media mutation through any of them
- [ ] A rejected mutation performs **no blob operation**: on a 403 (or 401) the request reaches
      neither `IStorageService` nor the repository — the check runs before any side effect
- [ ] Requests to gated routes carry no `Authorization` header → **401**, not 403; a valid token
      belonging to the wrong principal → **403**
- [ ] **403 is distinguishable from 404**: a non-owner mutating an activity that exists receives
      403; 404 is returned only when the activity genuinely does not exist, including for admins
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
  an allow/deny **matrix covering every route × role × ownership combination** from the PRD
  permission table: for each route, {anonymous, signed-in non-owner, owner, admin} × {activity
  exists, activity absent}. Assert the anonymous grant is limited to the two read operations and
  that every other anonymous cell denies. Assert a `User`-role caller carrying an admin-shaped
  claim is denied admin override.
- Integration (`TrailBlaze.Api.Test`) — **hot spot (security)**: exercise each endpoint over the
  real pipeline with a test token, asserting the exact status code: 401 for unauthenticated,
  403 for authenticated-but-unauthorized, 404 for genuinely absent, 200/201/204 for allowed.
  Explicitly assert a non-owner cannot reach `POST /api/activities/{id}/cover`,
  `POST /api/activities/{id}/media`, `DELETE /api/media/{id}`, or `GET /api/media/{id}/url`, and
  that a denied request makes **no** call into the fake `IStorageService`.
- Integration (`TrailBlaze.Repository.Test`) — the `CreatedByUserId` lookup the ownership decision
  reads returns the right owner, including after an update.
- Regression guard: a test asserting the anonymous-allowed route set is exactly
  `GET /api/activities` and `GET /api/activities/{id}`, so a new endpoint added later without an
  explicit decision fails rather than silently defaulting open.

## Notes / non-goals

- No policy engine, no expression language, no permission table — the rules are the fixed matrix
  in the PRD, not data-driven grants.
- No dedicated admin screens (Decision #21): admin is elevated rights in the same routes and UI.
- No per-media or per-activity visibility toggles (Decisions #13/#14): the public/private split is
  per class of content, not per item.
- No changes to the data model — ownership already rides on `activities.CreatedByUserId`, which
  feature 04 created.
- This feature does not add authentication itself; it consumes the identity and role that 02 and
  03 established.
