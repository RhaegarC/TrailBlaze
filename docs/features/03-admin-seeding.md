# 03 — Admin Seeding

Status: **Done (2026-09-19)** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #9/#21 + "Authentication & authorization" and the `users` data-model row.

## Summary

The `users.Role` column, defaulting to `User`, plus exactly one `Admin` seeded at startup from
configuration. Seeding is idempotent, so restarts and redeploys never produce a second admin or
silently reset the existing one. The role is read from the database and surfaced to the
authorization path — the token may say anything; the row is what counts.

**State at the start (2026-09-15).** The `Role` property existed on the `User` entity, but as a
nullable `string` with no default, no length, and no constraint to `User`/`Admin` — so the column was
present and the *rule* was not. Nothing seeded an admin: there was no startup seeding path at all.

**State now.** The column is non-null, defaulted to `User`, bounded to 16 characters and
check-constrained to the closed set by `CK_Users_Role`; a migration backfills and tightens it. The
configured administrator is seeded once at startup and is idempotent across restarts. The caller's
role is answered from their own row by `ICallerRoleService` — with one deliberate divergence from
this file's original wording, recorded under [Decisions](#decisions) below.

## Story

As the operator of the journal I want exactly one administrator provisioned from configuration at
startup so that elevated rights exist without a grant screen, an invitation flow, or a manual SQL
edit.

## Dependencies

- [02-entra-auth](archive/02-entra-auth.md) (`users` table and provisioning, caller identity abstraction).
  **The `users` key shape is settled** — `users.Id` *is* the Entra object id, and there is no
  `EntraObjectId` column — so seeding a row means choosing the `oid` it will answer to, and the
  seeded admin is reached by whoever signs in with that object id. Note that 02's profile tests
  were deferred ([02-entra-auth.md](archive/02-entra-auth.md#testing-status)); the schema 03 builds on is
  migrated but its behaviour is not yet proven by tests.

## Acceptance criteria

- [x] `users.Role` is constrained as the PRD specifies: non-null, defaulting to `User`, with values
      limited to `User` and `Admin` (PRD Decision #9). The property already exists on the entity as
      a nullable `string`, so this is a constraint change rather than a new column —
      `TrailBlazeContext` maps it `HasMaxLength(16).HasDefaultValue(User).IsRequired()` plus
      `CK_Users_Role`, and `UserModelTests` asserts all four against the design-time model
- [x] A migration backfills existing rows to `User` and tightens the column — applied by the
      deployment pipeline like every other migration, not at API startup (see
      [01-foundation.md](archive/01-foundation.md) for why the startup path was removed).
      `20260919032941_ConstrainUserRole` hand-orders its three steps — backfill, `AlterColumn`,
      constraint — because each is refused by the state the one before it removes; verified against
      a populated database, not only an empty one. Applied by the pipeline, unchanged from 01:
      `Program.cs` still carries no `MigrateAsync` call
- [x] Startup seeds exactly one `users` row with `Role = Admin`, taking the Entra object id and
      `DisplayName` from configuration. `Email` was not added — the PRD's open column decision did
      not land in this feature, and the seeded row has none. `AdminSeedingService.SeedAdminAsync`
- [x] Seeding is idempotent: N restarts against the same database leave exactly one admin row —
      no duplicate, no reset of that row's other columns, no failure. Read-before-write, so the
      second run issues no write at all: `AdminSeedingTests.Seeding_a_second_time_writes_nothing`
      asserts an unchanged `LastModifiedOn` and exactly one audit entry, and a real host booted
      twice logged `Inserted` then `AlreadyAdmin` with one row and one audit entry in the database
- [x] If the configured admin's `oid` already exists, seeding promotes that row to `Admin` rather
      than inserting alongside it or failing —
      `AdminSeedingTests.Seeding_promotes_an_existing_row_without_resetting_it` pre-loads a `User`
      row and asserts every other column survives
- [x] A missing or empty admin configuration fails startup with a message naming the setting rather
      than booting an adminless system (consistent with 01's configuration rule) —
      `ServiceExt.RequireAdminSeed`, called from `Program`, throwing before the host is built;
      `StartupTests` covers both keys by name
- [x] The role is resolved from the `users` row keyed by the caller's Entra object id — never from
      a token claim (PRD "Authentication & authorization"). `CallerRoleService` reads the object id
      and nothing else; `CallerRoleTests` covers the row-says-`User`, no-row and no-identity cases
- [x] ~~The identity abstraction from 02 exposes `Role`, so a later authorization check has exactly
      one place to ask~~ — **not done as worded; see [Decisions](#decisions).** The role is exposed
      by a new `ICallerRoleService` instead, and `IUserContextService` is asserted to still have no
      `Role` (`RoleComesFromTheRowTests`)
- [x] A user auto-provisioned by 02's path is inserted with `Role = User`, and no request payload
      or claim can make it `Admin`. The column default covers any insert that does not name it, and
      `RoleComesFromTheRowTests.The_profile_edit_request_offers_no_role` asserts `UpdateProfileRequest`
      has no `Role` property for a payload to carry
- [x] A token carrying an admin-looking claim does not grant admin when the caller's row says `User`.
      Structural rather than behavioural: the claim is not read anywhere, because the type that
      could read it exposes no role to read. There is no test that plants an admin claim and watches
      it be ignored, and there cannot be a meaningful one while `IUserContextService` has no role to
      assert on — the absence is the guarantee, and it is asserted upstream
- [x] Seeding tolerates being run twice in the same process lifetime without a duplicate insert.
      N invocations leave one row; two *concurrent* hosts are a different case, argued in
      `AdminSeedingService`'s remarks and accepted rather than solved

## Tests (TDD)

The three bullets this section carried when the feature was written predated
[PR #10](https://github.com/RhaegarC/TrailBlaze/pull/10), which gave the repository and storage
tiers real containers to talk to. They said "No database is required" and put the store-backed
tests in `TrailBlaze.Repository.Test`; both were true of the harness as it then stood and are
wrong now. What was actually written, by tier:

- **Model (`TrailBlaze.Repository.Test`, offline).** `UserModelTests` gained
  `The_role_column_is_non_nullable_and_defaults_to_user` and
  `The_role_column_admits_only_the_closed_set`. The second reads the **design-time** model —
  EF 10 keeps check constraints out of the read-optimized runtime model, so asking `Context.Model`
  throws. Both assert against literals rather than against `Constant.UserRole.All`, so a change to
  the set fails here instead of agreeing with itself.
- **Migration (`TrailBlaze.Repository.Test`, `Category=Container`).** `UserRoleMigrationTests`
  migrates from `InitialCreate` and asserts what only an engine can: pre-existing NULL and
  out-of-set rows come out as `User` while a pre-existing `Admin` is preserved, the column ends up
  `IS_NULLABLE = 'NO'`, the engine itself refuses a third role with SQL error 547 naming
  `CK_Users_Role`, and a bare `INSERT` with no `Role` lands as `User`.
- **Service (`TrailBlaze.Service.Test`).** No longer empty. `AdminSeedingTests` (container) covers
  insert, idempotence-by-audit-entry, and promotion-preserving-every-other-column;
  `CallerRoleTests` (container) covers the row, the missing row, the missing identity and the
  soft-deleted row. `RoleComesFromTheRowTests` (offline) is the reflection assertion that the role
  has nowhere else to come from. This project gained a `ProjectReference` to
  `TrailBlaze.Repository.Test` to reach `TestSupport/` — see [Decisions](#decisions).
- **API (`TrailBlaze.Api.Test`, offline).** `StartupTests` names both new settings in its existing
  "startup fails naming the missing key" theory, plus the over-long-`DisplayName` case;
  `CompositionRootTests` resolves both new contracts. `TrailBlazeApiFactory` supplies the two
  settings, which is what makes it the regression test for the offline property: the seeder now
  makes one failing connection attempt at boot, and the suite still passes with no database.

What the tests still cannot speak to is deployment shape — one container is not several ACA
replicas, and no test signs in through Entra. See [testing-and-tdd.md](../testing-and-tdd.md).

## Decisions

Three choices this feature made that differ from what the file, or a neighbouring document,
originally said. Each is recorded here rather than only in code.

1. **The role is a service, not a property on `IUserContextService`.** The criterion above asked
   for `Role` on 02's identity abstraction. That abstraction is synchronous and claims-only and is
   implemented in the Api layer; a role read from the row would have put data access in the
   entrance and made the Api layer reach back into the Service layer, which is a cycle the solution
   is not built on. `ICallerRoleService.GetRoleAsync` sits in `TrailBlaze.Interface/Service/` and is
   implemented in `TrailBlaze.Service`. The consequence is that there are now **two** ways to ask
   about the caller and a reader has to know which answers what — worth the price, because the
   alternative was a layer violation, and `RoleComesFromTheRowTests` pins the split so it cannot
   quietly reunite.
2. **Configuration absence is fatal; database absence is not.** A missing or over-long admin setting
   stops the host at the composition root, naming the key: a deployment nobody can administer must
   not start looking healthy. An unreachable database is caught broadly in
   `AdminSeedingHostedService`, logged at error, and survived — a replica that crash-loops here
   never gets to seed on a later attempt without a redeploy, and it could not serve a request
   anyway. The cost is stated rather than hidden: the API can be up with no administrator in it,
   and the only signal is that error line. This is the one place the feature knowingly leaves a
   state that a health check would call healthy.
3. **Store-backed service tests run in `TrailBlaze.Service.Test`.** That project gained a
   `ProjectReference` to `TrailBlaze.Repository.Test` to reuse its fixtures and `TestSupport/`.
   [Tech-debt 25](../tech-debt/25-service-test-tier-is-empty.md) already owned this question and
   recommends the opposite split; taking the other option is recorded there rather than diverged
   from in silence. The tier rule is "new tests go in the project matching the layer they exercise",
   the seeding and role logic is service-layer logic, and the measured cost is small — `Service.Test`
   still runs offline in milliseconds, skipping its seven container tests.

## Notes / non-goals

- **No admin screens.** The role is elevated rights inside the same UI, not a separate surface
  (PRD Decision #21).
- **No role management API** — no endpoint promotes, demotes, lists, or deletes users. The single
  admin is a configuration fact, which is what keeps the privilege auditable.
- **No enforcement here.** This feature exposes the role; feature 09 is what denies a non-owner
  and what grants the admin override.
- Multiple admins are deliberately unsupported: the PRD says exactly one is seeded. Adding a second
  is a change to this decision, not a configuration tweak.
- The seeded admin's Entra object id must correspond to a real Entra object, or the seed row is
  simply never claimed by a sign-in; that is an operator concern, not something this feature
  validates against the graph.
- **A soft-deleted row for the configured id is a stuck state.** The seeder's lookup goes through
  the same global query filter as everything else, so its row is invisible; inserting then fails on
  the primary key, and the operator gets an error line on every start until someone edits the row in
  SQL. Verified against a real database rather than reasoned about, and filed as
  [tech-debt 28](../tech-debt/28-soft-deleted-admin-blocks-seeding.md) rather than solved here,
  because solving it means deciding whether an administrator can be un-deleted, which is a policy
  question this feature does not own. Nothing in the product deletes a user yet, so it is reachable
  only by hand for now.
- **Two replicas starting at once is accepted, not solved.** Both read no row, both insert, one wins
  on the primary key and the other logs loudly and leaves the state correct — the same trade
  `UserService.GetOrCreateAsync` already makes. Serialising it would need a lock or an upsert that
  the repository does not offer, to fix a window that only opens on a cold deployment.
