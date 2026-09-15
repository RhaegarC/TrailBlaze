# 02 — Entra Auth

Status: **In progress** — validation and provisioning exist; hardening outstanding · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decision #8 + "Authentication & authorization" and the `users` data-model row.

## Summary

Entra ID bearer validation and caller identity. The API validates the token against the configured
tenant, extracts the `oid` and display-name claims, and **auto-provisions** a `users` row the first
time it sees a new `oid` — there is no registration step and no local password. The resulting
identity is handed to services through an abstraction so that no service reads `HttpContext`.

### Current state (2026-09-15)

The scaffold already implements the core of this feature. What exists:

| Already present | Where |
|---|---|
| Bearer validation against `TenantId` + `Audience`, with `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all on | `ServiceExt.AddEntraAuthentication` |
| Anonymous-by-default when tenant or audience is unset, with a startup warning saying so rather than a silent half-configuration | `Program.cs` |
| Caller abstraction, read-only, resolvable without a request and returning nulls rather than throwing | `IUserContextService` / `UserContextService` |
| `oid` extraction with fallback to the long `.../objectidentifier` schema claim; display name falling back from `name` to `preferred_username` | `UserContextService` |
| Auto-provisioning on first sight, keyed on the object id, returning null rather than inserting when the token carries none | `UserService.GetOrCreateAsync` |
| `GET /user/me` as the authenticated route that triggers provisioning | `UserController` |

Three things the code does **not** yet do, and one it does differently:

- **No email claim is read or stored.** The `users` table has `DisplayName` and `Role` only.
- **No truncation to column lengths**, so an over-long claim is not defended against.
- **No concurrency handling.** Provisioning is get-then-insert with no unique constraint behind it,
  so a duplicate-key race between two first requests surfaces as a 500 rather than converging.
- **The abstraction is `IUserContextService`, not `ICurrentUser`, and it exposes the Entra `oid`
  rather than an internal `users.Id`** — because the code uses the object id *as* the primary key.
  That key-shape difference is flagged as an open decision in the PRD's "Current state vs. target";
  settle it before treating the acceptance criteria below as final.

## Story

As a signed-in user I want my Entra sign-in to be recognised on my first request so that I am
attributed for what I write without registering, inviting, or waiting for an admin to create me.

## Dependencies

- [01-foundation](01-foundation.md) (layered solution, Azure SQL Server via EF Core, configuration
  binding for the Entra tenant id and audience, `/health`)

## Acceptance criteria

### Already satisfied — verify, do not rebuild

- [x] The API validates Entra ID bearer tokens against the configured tenant and audience; a
      missing, malformed, expired, or wrong-tenant token on a protected route returns 401
      (PRD Decision #8)
- [x] `GET /health` remains reachable with no token
- [x] Claim extraction has a documented fallback order: `oid`, then the long
      `.../objectidentifier` schema claim; display name from `name`, then `preferred_username`
- [x] Caller identity is available to services through an abstraction
      (`IUserContextService`) and no service touches `HttpContext`; it is unit-testable without a
      request by substituting the interface
- [x] The abstraction reports "no caller" rather than throwing when the request is anonymous or
      absent, so the two public read endpoints can consult it safely
- [x] An unauthenticated request to a protected route is rejected before any database write occurs
      — `[Authorize]` short-circuits ahead of the service call
- [x] Provisioning writes only the `users` table; it creates no activities or media rows

### Outstanding

- [ ] On the first authenticated request carrying an unseen `oid`, a `users` row is inserted and
      the row's identity is the object id. **Pending the PRD's open key-shape decision**, either
      keep the current `users.Id = oid` or add a surrogate `Id` plus a unique `EntraObjectId`
      column; the criterion is whichever shape wins, enforced so that one `oid` maps to exactly
      one row
- [ ] A second request with the same object id neither inserts a second row nor fails
- [ ] Concurrent first requests for the same object id converge on exactly one row, and a
      duplicate-key race surfaces as a normal authenticated request rather than a 500
- [ ] Values are truncated to the column lengths before insert so an over-long claim cannot fail
      the insert (`DisplayName` 200, and `Role` 16 if a column length is set on it)
- [ ] An email claim is captured, or the decision not to store one is recorded — the PRD's
      `users` row lists `Email` and the code currently reads no email claim at all

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) — **hot spot (identity)**: provisioning is idempotent (same
  `oid` twice → one row); a missing optional claim falls back per the documented order; an
  anonymous caller yields no caller and triggers no write; an over-long claim is truncated, not
  thrown on.
- Integration (`TrailBlaze.Repository.Test`): the uniqueness constraint behind the object id holds
  — a second insert for the same id is rejected — and get-by-object-id returns the provisioned row
  while returning nothing for an unknown one. Runs with no database, per the no-database pattern
  ([testing-and-tdd.md](../testing-and-tdd.md)).
- Integration (`TrailBlaze.Api.Test`) — **hot spot (security)**: with a test authentication scheme
  standing in for Entra, no token → 401; a valid token → 200 and exactly one `users` row; the same
  token a second time → still one row. The 401 is asserted to occur with no `users` insert, which
  is what proves rejection precedes the write.

## Notes / non-goals

- **No role logic.** Every provisioned user is a plain user; the `Role` column and its default
  arrive in 03, and nothing is `[Authorize(Roles = ...)]` until then.
- **No authorization rules.** Any authenticated caller can reach every non-anonymous route.
  Ownership and admin rules are feature 09.
- No token acquisition, refresh, or sign-out — MSAL runs in the browser and is wired at feature 10.
- No user profile editing, invitations, deactivation, or a users API; a `users` row exists only
  because someone signed in.
- The token is used for identity only. It is never the source of privilege — the database decides
  that, which is why 03 stores the role in a column (PRD "Authentication & authorization").
