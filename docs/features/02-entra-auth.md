# 02 — Entra Auth

Status: **Implemented — tests deferred** — every acceptance criterion below is met by code, but the
test coverage described in [Testing status](#testing-status) was deliberately not written in this
pass, so the feature is **not** finished by [STANDARD.md](../../src/api/STANDARD.md) §10 ·
[00-mission-1-sprint.md](00-mission-1-sprint.md)
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

### Closed in this pass (2026-09-16)

- **The email claim is read and stored.** `IUserContextService` gained `Email`, read from the
  `email` claim with the WS-Federation `emailaddress` fallback. `preferred_username` is
  deliberately *not* an email fallback, even though it often holds an address: it already serves as
  the display-name fallback and a tenant may configure it to a phone number, so copying it into an
  `Email` column would store a value the column's name asserts to be something it may not be.
- **Token claims are shortened to their column lengths.** The lengths live in
  `Constant.UserProfile` and are read by both the mapping and the service, so the bound and the
  truncation cannot drift.
- **Concurrent provisioning converges.** `IUserRepository.AddIfAbsentAsync` inserts and treats a
  duplicate-key violation (SQL Server 2601/2627) as "already there"; the service then re-reads the
  winner's row. Recognising the error code is data-access knowledge, so the catch is in the
  repository rather than the service.
- **The abstraction exposes the Entra `oid`, and `users.Id` **is** that `oid`.** The PRD's
  proposed surrogate key plus `EntraObjectId` column was rejected in favour of the code's shape;
  the PRD's data model and its divergence note now describe the code. See [PRD](../../PRD.md)
  "Current state vs. target".
- **The profile slice (Decision #28) is implemented**: four profile routes, request/response DTOs,
  a shared upload validator, and avatar storage in the public container.

## Story

As a signed-in user I want my Entra sign-in to be recognised on my first request so that I am
attributed for what I write without registering, inviting, or waiting for an admin to create me.

## Dependencies

- [01-foundation](archive/01-foundation.md) (layered solution, Azure SQL Server via EF Core, configuration
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

### Provisioning and hardening (completed 2026-09-16)

- [x] On the first authenticated request carrying an unseen `oid`, a `users` row is inserted and
      the row's identity is the object id. **The key-shape decision is settled**: `users.Id = oid`,
      the PRD's proposed surrogate `Id` + `EntraObjectId` was rejected, and the primary key is what
      enforces one row per `oid`
- [x] A second request with the same object id neither inserts a second row nor fails
- [x] Concurrent first requests for the same object id converge on exactly one row, and a
      duplicate-key race surfaces as a normal authenticated request rather than a 500 —
      `UserRepository.AddIfAbsentAsync`
- [x] Values are truncated to the column lengths before insert so an over-long claim cannot fail
      the insert (`DisplayName` 200, `Email` 320, `Role` 16)
- [x] An email claim is captured from `email`, with the WS-Federation `emailaddress` fallback

### Profile and preferences

Added 2026-09-15 from the Figma export's profile screen (Decision #28). The screen's fields are the
source of these criteria; the avatar is the only one with a storage story.

- [x] `GET /user/me` returns the whole row — `Id`, `Email`, `DisplayName`, `Role`, `Description`,
      the avatar as the **resolved public URL**, and both preferences — so the profile screen
      populates from one request rather than several. The response is `UserProfileResponse`, not
      the entity: the stored blob path is an internal detail and is never handed out
- [x] `PUT /user/me` updates `DisplayName`, `Description`, `PreferredTheme` and
      `PreferredLanguage` on the **caller's own row only**. The route carries no user id, so there
      is no parameter through which one caller could reach another's profile
- [ ] `PUT /user/me` never writes `Role`, `Email` or `Id`. Those fields are absent from the request
      model — **the guard is the absence**, and `Role` in particular must not be self-assignable,
      because a caller who could set it would grant themselves admin and defeat feature 03's
      seeding entirely (Decision #9). **The reflection test that asserts this shape does not exist
      yet** (see [Testing status](#testing-status)); the property is true of the code but unproven
- [x] `DisplayName` is required and non-blank, max 200 characters; `Description` is optional,
      accepts long text, and normalises a whitespace-only value to null — the same rule an
      activity's `Description` follows, so the two do not diverge
- [x] `PreferredTheme` accepts only `Dark` and `Light`; `PreferredLanguage` only `en` and `zh`.
      Anything else is rejected with 400, and both columns are **non-nullable with a default**
      (`Dark`, `en`) so no client ever has to decide what an absent preference means. These are
      presentation preferences only and carry no authorization meaning. An omitted preference
      resolves to its default rather than to "leave unchanged" — this replaces the editable fields
- [x] `POST /user/me/avatar` accepts an image on the same allowlist and size cap as a cover, writes
      it to the **public `avatars`** container through `IStorageRepository`, stores the path on the
      caller's row, and answers with the profile carrying the public URL
- [x] An avatar upload replaces any previous one and **deletes the old blob**, so exactly one avatar
      blob exists per user and none are orphaned. The delete runs *after* the row points at the new
      blob: the reverse order can leave the row naming a blob that is already gone, while this
      order can at worst leave one unreferenced blob
- [x] `DELETE /user/me/avatar` clears the field and deletes the blob; a caller who has no avatar
      gets a no-op success rather than a 404 or an error. "No-op" means **zero storage calls**, not
      merely a 200 — nothing is deleted that was never there
- [ ] **An avatar is public by design** (Decision #28) and is the one user-owned image that is: a
      plain unauthenticated HTTP GET against the returned URL returns the image, asserted in the
      tagged storage tier. **Not proven.** The code resolves the URL through
      `CreatePublicUrl` — unsigned, with no SAS — so the design is implemented, but nothing has
      fetched an avatar from the real container. This is exactly the opposite of an activity's
      media, and the contrast is deliberate — an avatar is an identity, not a record of a private
      day
- [x] The profile routes are reachable only when authenticated: **401** anonymously, and there is
      no route by which one caller reads or writes another's profile. `[Authorize]` sits on
      `UserController` rather than on each action, so a future profile route inherits it. This is
      the only controller in the application, so `GET /health` (and the OpenAPI document, which is
      development-only) is now the whole of what is reachable without a token — `GET /user/index`
      included, which took no such decision but inherits the type's rule

## Testing status

**The tests for this feature were not written.** Every acceptance criterion above is satisfied by
code, and the code compiles and the existing 41 tests still pass — but the coverage this feature
asks for was deliberately skipped in this pass, so what exists is *unproven* rather than *verified*.
Two acceptance criteria are checked above as unmet for exactly this reason, and they are the two
that matter most:

- **The privilege-escalation guard has no test.** `UpdateProfileRequest` has no `Role`, `Email` or
  `Id` property, and that absence is the entire control. It is true of the code today and nothing
  would fail if a later field were added to the DTO. The planned reflection test is what makes the
  guard durable, and it does not exist.
- **The avatar's public-read behaviour has no test**, so the design intent behind Decision #28 is
  implemented but unfetched.

This departs from [STANDARD.md](../../src/api/STANDARD.md) §10 — *a behaviour change without a test
is not finished*. **By that rule this feature is not finished**, whatever its status line says, and
it should not be treated as the baseline that 03 builds on until the tests below exist. Recording
this here is the point: an untested guard that everyone believes is tested is worse than one known
to be untested, because the second gets written.

### Three things no offline test can prove, whichever pass writes them

1. **The duplicate-key race.** With no database, the `SqlException` catch in `AddIfAbsentAsync` is
   never entered. A unit test can stub `AddIfAbsentAsync` returning `false` and assert the service
   re-reads — that proves the service's branch, not that the catch recognises what SQL Server
   actually throws. The catch is verified by inspection and belongs to feature 11.
2. **The narrowing `ALTER COLUMN` against a populated table.** `AddUserProfileColumns` narrows
   `Role`, `DisplayName` and `Description` from `nvarchar(max)`. Offline we can assert the model's
   lengths; whether SQL Server accepts the change on existing rows depends on the data it finds, and
   no environment is deployed yet. A populated database that predates this migration would need
   over-long values shortened first.
3. **The public-read container.** The storage tier skips without `TRAILBLAZE_STORAGE_CONNECTION`,
   so a default `dotnet test` does not exercise it, and no test asserts the container is
   genuinely public-read.

These route to feature 11 rather than being ticked here.

## Tests (TDD)

None of the following are written yet — they are the plan, kept as a specification for the pass
that writes them.

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
