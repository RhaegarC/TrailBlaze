# 02 — Entra Auth

Status: **In progress** — validation and provisioning exist; hardening outstanding · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #8/#28 + "Authentication & authorization" and the `users` data-model row.

## Summary

Entra ID bearer validation, caller identity, and the caller's own profile. The API validates the
token against the configured tenant, extracts the `oid` and display-name claims, and
**auto-provisions** a `users` row the first time it sees a new `oid` — there is no registration
step and no local password. The resulting identity is handed to services through an abstraction so
that no service reads `HttpContext`.

The profile slice (added 2026-09-15, Decision #28) extends this feature rather than forming a new
one, because every part of it is a statement about the `users` row this feature already owns:
display name, bio, avatar, and the two presentation preferences. It is **strictly self-service** —
a caller may edit their own row and no other, and may not touch `Role`.

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

- **No email claim is read or stored.** The `users` table carries `DisplayName`, `Role` and
  `Description`; the profile screen displays an email, so this is now a visible gap in the UI's
  data rather than only a missing column.
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

### Profile and preferences

Added 2026-09-15 from the Figma export's profile screen (Decision #28). The screen's fields are the
source of these criteria; the avatar is the only one with a storage story.

- [ ] `GET /user/me` returns the whole row — `Id`, `Email`, `DisplayName`, `Role`, `Description`,
      the avatar (path, or the resolved public URL) and both preferences — so the profile screen
      populates from one request rather than several
- [ ] `PUT /user/me` updates `DisplayName`, `Description`, `PreferredTheme` and
      `PreferredLanguage` on the **caller's own row only**. The route carries no user id, so there
      is no parameter through which one caller could reach another's profile
- [ ] `PUT /user/me` never writes `Role`, `Email` or `Id`. Those fields are absent from the request
      model, and a test asserts that shape rather than trusting it — **`Role` must not be
      self-assignable**, because a caller who could set it would grant themselves admin and defeat
      feature 03's seeding entirely (Decision #9)
- [ ] `DisplayName` is required and non-blank, max 200 characters; `Description` is optional,
      accepts long text, and normalises a whitespace-only value to null — the same rule an
      activity's `Description` follows, so the two do not diverge
- [ ] `PreferredTheme` accepts only `Dark` and `Light`; `PreferredLanguage` only `en` and `zh`.
      Anything else is rejected with 400, and both columns are **non-nullable with a default**
      (`Dark`, `en`) so no client ever has to decide what an absent preference means. These are
      presentation preferences only and carry no authorization meaning
- [ ] `POST /user/me/avatar` accepts an image on the same allowlist and size cap as a cover, writes
      it to the **public `avatars`** container through `IStorageService`, stores the path on the
      caller's row, and returns the public URL
- [ ] An avatar upload replaces any previous one and **deletes the old blob**, so exactly one avatar
      blob exists per user and none are orphaned
- [ ] `DELETE /user/me/avatar` clears the field and deletes the blob; a caller who has no avatar
      gets a no-op success rather than a 404 or an error
- [ ] **An avatar is public by design** (Decision #28) and is the one user-owned image that is: a
      plain unauthenticated HTTP GET against the returned URL returns the image, asserted in the
      tagged storage tier. This is exactly the opposite of an activity's media, and the contrast is
      deliberate — an avatar is an identity, not a record of a private day
- [ ] The profile routes are reachable only when authenticated: **401** anonymously, and there is
      no route by which one caller reads or writes another's profile

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) — **hot spot (identity)**: provisioning is idempotent (same
  `oid` twice → one row); a missing optional claim falls back per the documented order; an
  anonymous caller yields no caller and triggers no write; an over-long claim is truncated, not
  thrown on.
- Integration (`TrailBlaze.Repository.Test`): the uniqueness constraint behind the object id holds
  — a second insert for the same id is rejected — and get-by-object-id returns the provisioned row
  while returning nothing for an unknown one. Runs with no database, per the no-database pattern
  ([testing-and-tdd.md](../testing-and-tdd.md)).
- Unit (`TrailBlaze.Service.Test`) — **hot spot (privilege escalation):** the profile update model
  carries no `Role` field, and a `PUT /user/me` body attempting to set `Role`, `Email` or `Id`
  leaves the stored `Role` unchanged. Asserted as a RED-first test because the failure it guards
  against — a user promoting themselves to admin — is silent, permanent, and defeats feature 03.
  Also the preference enums: an out-of-range theme or language is rejected, and both default when
  omitted.
- Unit (`TrailBlaze.Service.Test`): avatar upload validation reuses the cover allowlist and size
  cap; replacing an avatar deletes the previous blob; removing an avatar with none present is a
  no-op success rather than an error.
- Integration (`TrailBlaze.Repository.Test`): the new `users` columns round-trip through the
  `DbContext` with no database — `PreferredTheme`/`PreferredLanguage` default rather than persist
  as null, and `Description` normalises whitespace to null ([testing-and-tdd.md](../testing-and-tdd.md)).
- Integration (`TrailBlaze.Api.Test`) — **hot spot (security)**: with a test authentication scheme
  standing in for Entra, no token → 401; a valid token → 200 and exactly one `users` row; the same
  token a second time → still one row. The 401 is asserted to occur with no `users` insert, which
  is what proves rejection precedes the write.
- Integration (`TrailBlaze.Api.Test`): `GET /user/me` returns the profile fields; `PUT /user/me`
  persists a display-name and bio change that is visible on the next read; an anonymous request to
  every profile route returns 401.
- Storage integration (`TrailBlaze.Service.Test`, tagged `Category=StorageIntegration`): an avatar
  written to the real `avatars` container is retrievable by a **credential-free** HTTP GET. That is
  the assertion that the container is genuinely public-read, which is the whole design intent for
  avatars and cannot be shown with the fake.

## Notes / non-goals

- **No role logic.** Every provisioned user is a plain user; the `Role` column and its default
  arrive in 03, and nothing is `[Authorize(Roles = ...)]` until then.
- **No authorization rules.** Any authenticated caller can reach every non-anonymous route.
  Ownership and admin rules are feature 09.
- No token acquisition, refresh, or sign-out — MSAL runs in the browser and is wired at feature 10.
- **Invitations and deactivation remain out of scope.** A `users` row still exists only because
  someone signed in, and there is no way to create, invite, or deactivate a user from the app.
  Profile *editing* and a users API did belong on this list and no longer do (Decision #28): what
  exists is strictly **self-service editing of the caller's own row**, never administration of
  another's.
- The token is used for identity only. It is never the source of privilege — the database decides
  that, which is why 03 stores the role in a column (PRD "Authentication & authorization").
