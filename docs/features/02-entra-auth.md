# 02 — Entra Auth

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decision #8 + "Authentication & authorization" and the `users` data-model row.

## Summary

Entra ID bearer validation and caller identity. The API validates the token against the configured
tenant, extracts the `oid`/email/display-name claims, and **auto-provisions** a `users` row the
first time it sees a new `oid` — there is no registration step and no local password. The resulting
identity is handed to services through an abstraction so that no service reads `HttpContext`.

## Story

As a signed-in user I want my Entra sign-in to be recognised on my first request so that I am
attributed for what I write without registering, inviting, or waiting for an admin to create me.

## Dependencies

- [01-foundation](01-foundation.md) (layered solution, SQL Server via EF Core, configuration
  binding for the Entra tenant and client ids, `/health`)

## Acceptance criteria

- [ ] The API validates Entra ID bearer tokens against the configured tenant and audience; a
      missing, malformed, expired, or wrong-tenant token on a protected route returns 401
      (PRD Decision #8)
- [ ] `GET /health` remains reachable with no token
- [ ] On the first authenticated request carrying an unseen `oid`, a `users` row is inserted with
      `EntraObjectId` = `oid`, `Email`, `DisplayName`, and `CreatedUtc`
- [ ] A second request with the same `oid` neither inserts a second row nor fails; `EntraObjectId`
      carries a unique constraint
- [ ] Concurrent first requests for the same `oid` converge on exactly one row, and a duplicate-key
      race surfaces as a normal authenticated request rather than a 500
- [ ] Claim extraction has a documented fallback order, so a token missing `name` or
      `preferred_username` still provisions a usable row (display name falls back to email, email
      to empty) without throwing
- [ ] Values are truncated to the column lengths before insert (`EntraObjectId` 64, `Email` 320,
      `DisplayName` 200) so an over-long claim cannot fail the insert
- [ ] Caller identity is available to services through an abstraction (`ICurrentUser` or
      equivalent) exposing the internal `users.Id`, `EntraObjectId`, `Email`, and `DisplayName`;
      no service touches `HttpContext`, and the abstraction is unit-testable without a request
- [ ] The abstraction reports "no caller" rather than throwing when the request is anonymous, so
      the two public read endpoints can consult it safely
- [ ] An unauthenticated request to a protected route is rejected before any database write occurs
- [ ] Provisioning writes only the `users` table; it creates no activities or media rows

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) — **hot spot (identity)**: provisioning is idempotent (same
  `oid` twice → one row); a missing optional claim falls back per the documented order; an
  anonymous caller yields no caller and triggers no write; an over-long claim is truncated, not
  thrown on.
- Integration (`TrailBlaze.Repository.Test`): `EntraObjectId` unique index behaves against SQL
  Server; get-by-`oid` returns the provisioned row and returns nothing for an unknown `oid`.
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
