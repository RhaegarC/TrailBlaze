# 03 — The role column, and the single admin

Status: **Done (2026-09-19)** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #9/#21 + "Authentication & authorization" and the `users` data-model row.

> **The file keeps its name.** It was written as *admin seeding*, and feature numbers are identity
> here rather than a description — the sprint file, the PRD, [item 25](../tech-debt/25-service-test-tier-is-empty.md)
> and [feature 09](09-permission-enforcement.md) all link to this path — so the slug is left alone
> and the heading above says what the feature now is. What changed, and why, is under
> [Decisions](#decisions).

## Summary

The `users.Role` column: non-null, defaulting to `User`, bounded, and constrained to the closed set
`User`/`Admin`. The administrator is a **database fact** — one row with `Role` set to `Admin`, set by
hand, by whoever operates the deployment. Nothing in the application grants, promotes, or seeds it,
and nothing reads a role from a token: the role is resolved from the caller's own row.

**State at the start (2026-09-15).** The `Role` property existed on the `User` entity, but as a
nullable `string` with no default, no length, and no constraint to `User`/`Admin` — so the column was
present and the *rule* was not.

**State now.** The column is non-null, defaulted to `User`, bounded to 16 characters and
check-constrained to the closed set by `CK_Users_Role`; `20260919032941_ConstrainUserRole` backfills
and tightens it. The caller's role is answered from their own row by `ICallerRoleService`. There is
no seeding path, no admin configuration key, and no hosted service — see
[Decisions](#decisions), which records that removal.

## Story

As the operator of the journal I want the administrator to be a row in my own database, so that
elevated rights exist without a grant screen, an invitation flow, or an application that writes to
`Role` on its own.

## Dependencies

- [02-entra-auth](archive/02-entra-auth.md) (`users` table and provisioning, caller identity abstraction).
  **The `users` key shape is settled** — `users.Id` *is* the Entra object id, and there is no
  `EntraObjectId` column — so the administrator is reached by whoever signs in with the object id
  on that row. Note that 02's profile tests were deferred
  ([02-entra-auth.md](archive/02-entra-auth.md#testing-status)); the schema 03 builds on is migrated
  but its behaviour is not yet proven by tests.

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
- [x] The administrator is granted by updating one row's `Role` column to `Admin`, which the column
      admits and nothing else produces. `UserRoleMigrationTests.The_engine_refuses_a_role_outside_the_closed_set`
      and `The_database_applies_the_user_default_itself` bracket this from the database's side: the
      only two reachable values are the ones a hand edit chooses between

**Removed on 2026-09-19, not deferred.** Four criteria this feature carried were deleted rather than
postponed, because the behaviour they described was deleted too — every one of them was about the
startup seeder:

- ~~Startup seeds exactly one `users` row with `Role = Admin`, taking the Entra object id and
  `DisplayName` from configuration~~ (`AdminSeedingService.SeedAdminAsync`)
- ~~Seeding is idempotent: N restarts against the same database leave exactly one admin row~~ and
  ~~tolerates being run twice in the same process lifetime~~ (`AdminSeedingTests`)
- ~~If the configured admin's `oid` already exists, seeding promotes that row to `Admin` rather than
  inserting alongside it or failing~~
- ~~A missing or empty admin configuration fails startup with a message naming the setting~~
  (`ServiceExt.RequireAdminSeed`, `StartupTests`)

`AdminSeed`, `AdminSeedingService`, `IAdminSeedingService`, `AdminSeedingHostedService`,
`AdminSeedingTests`, the `AdminObjectId`/`AdminDisplayName` keys and the length check on the seeded
display name are all gone with them. Their removal takes a test count with it: this feature now adds
**11 tests**, where the seeder's version added 17.

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
- **Service (`TrailBlaze.Service.Test`).** No longer empty. `CallerRoleTests` (container) covers the
  row, the missing row, the missing identity and the soft-deleted row. `RoleComesFromTheRowTests`
  (offline) is the reflection assertion that the role has nowhere else to come from. This project
  gained a `ProjectReference` to `TrailBlaze.Repository.Test` to reach `TestSupport/` — see
  [Decisions](#decisions).
- **API (`TrailBlaze.Api.Test`, offline).** `CompositionRootTests` resolves `ICallerRoleService`,
  which is the one registration this feature adds. The startup theory is back to the two connection
  strings: with no admin keys to require, there is no longer a configuration mistake that this tier
  knows about and the composition root does not.

What the tests still cannot speak to is deployment shape — one container is not several ACA
replicas, and no test signs in through Entra. See [testing-and-tdd.md](../testing-and-tdd.md).

## Decisions

Four choices this feature made that differ from what the file, or a neighbouring document,
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
2. **The administrator is a database fact, not a startup behaviour.** An earlier revision of this
   feature seeded the configured administrator from two required settings at boot — a hosted
   service, a read-then-insert-then-promote service, and a configuration path that refused to start
   without an admin. All of it was removed on 2026-09-19, and the reason is the whole argument:
   **elevating an account is a decision an operator makes about their deployment, not something an
   application should do to its own data at startup.** The seeder meant every boot carried a write
   path into a security column; it needed a setting whose absence had to be argued as a fatal
   configuration state rather than a working default; and it produced two failure modes nobody
   wants — a replica that boots healthy with no administrator because the database was briefly
   unreachable, and a soft-deleted row for the configured id that blocks every subsequent start
   (found by running it, and filed as debt 28 against the seeder). Removing the seeder removed the
   defect with it, and item 28 was deleted rather than fixed.
   What replaces it is one `UPDATE` a person runs against the deployed database. That is a weaker
   guarantee in one respect — nothing stops a deployment from having no administrator — and the
   trade is taken deliberately: the failure is visible the first time an admin action is attempted,
   and it is visible to the person who owns the credential, rather than being a silent rewrite of a
   privilege column that no one asked for. The role is still read from the row, which is what
   feature 09 needs and what this feature's remaining criteria assert.
3. **Store-backed service tests run in `TrailBlaze.Service.Test`.** That project gained a
   `ProjectReference` to `TrailBlaze.Repository.Test` to reuse its fixtures and `TestSupport/`.
   [Tech-debt 25](../tech-debt/25-service-test-tier-is-empty.md) already owned this question and
   recommends the opposite split; taking the other option is recorded there rather than diverged
   from in silence. The tier rule is "new tests go in the project matching the layer they exercise",
   the role logic is service-layer logic, and the measured cost is small — `Service.Test` still runs
   offline in milliseconds, skipping its four container tests.
4. **Nothing in the application writes `Admin`.** With the seeder gone there is no code path that
   sets `Role` to anything but the column's default: `UserService` inserts without naming the column
   and assigns four fields on update, none of them `Role`. The check constraint admits `Admin`
   because a hand edit must be able to write it — the constraint is there to refuse a *third* value,
   not to make `Admin` unreachable from the database. The structural guard is on the application
   side: `RoleComesFromTheRowTests` asserts `UpdateProfileRequest` carries no `Role`, which is where
   a promotion endpoint would have to start.

## Notes / non-goals

- **No admin screens.** The role is elevated rights inside the same UI, not a separate surface
  (PRD Decision #21).
- **No role management API, and no user-management module.** No endpoint promotes, demotes, lists or
  deletes users, and no startup path writes the column. A user-management module is plausible later;
  it is not this feature, and building it now would be inventing a surface with no requirement
  behind it. The administrator is one row edited by hand, which is also what keeps the privilege
  auditable — a change to it is a change someone made in SQL, not a side effect of a deploy.
- **No enforcement here.** This feature exposes the role; feature 09 is what denies a non-owner
  and what grants the admin override.
- Multiple administrators are deliberately unsupported: the PRD says one, and the column admits
  exactly the two values a single-admin design needs. Adding a second is a decision about
  authorization, not a configuration tweak.
- **The administrator's row must correspond to a real Entra object**, or it is simply never claimed
  by a sign-in. That is an operator concern, and the feature validates nothing against the graph —
  the same reason there is no seeding path to validate.
