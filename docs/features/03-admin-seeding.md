# 03 — Admin Seeding

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #9/#21 + "Authentication & authorization" and the `users` data-model row.

## Summary

The `users.Role` column, defaulting to `User`, plus exactly one `Admin` seeded at startup from
configuration. Seeding is idempotent, so restarts and redeploys never produce a second admin or
silently reset the existing one. The role is read from the database and surfaced to the
authorization path — the token may say anything; the row is what counts.

## Story

As the operator of the journal I want exactly one administrator provisioned from configuration at
startup so that elevated rights exist without a grant screen, an invitation flow, or a manual SQL
edit.

## Dependencies

- [02-entra-auth](02-entra-auth.md) (`users` table and provisioning, caller identity abstraction)

## Acceptance criteria

- [ ] `users.Role` is added as `nvarchar(16)`, non-null, defaulting to `User`, with values limited
      to `User` and `Admin` (PRD Decision #9)
- [ ] A migration adds the column to existing rows as `User` and is applied at startup like every
      other migration
- [ ] Startup seeds exactly one `users` row with `Role = Admin`, taking `EntraObjectId`, `Email`,
      and `DisplayName` from configuration
- [ ] Seeding is idempotent: N restarts against the same database leave exactly one admin row —
      no duplicate, no reset of that row's other columns, no failure
- [ ] If the configured admin's `oid` already exists, seeding promotes that row to `Admin` rather
      than inserting alongside it or failing
- [ ] A missing or empty admin configuration fails startup with a message naming the setting rather
      than booting an adminless system (consistent with 01's configuration rule)
- [ ] The role is resolved from the `users` row keyed by `EntraObjectId` — never from a token claim
      (PRD "Authentication & authorization")
- [ ] The identity abstraction from 02 exposes `Role`, so a later authorization check has exactly
      one place to ask
- [ ] A user auto-provisioned by 02's path is inserted with `Role = User`, and no request payload
      or claim can make it `Admin`
- [ ] A token carrying an admin-looking claim does not grant admin when the caller's row says `User`
- [ ] Seeding tolerates being run twice in the same process lifetime without a duplicate insert

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) — **hot spot (security)**: role resolution reads the database row
  and ignores token claims (a token asserting `Admin` for a `User` row resolves to `User`); the
  seeding decision is idempotent across repeated invocations; a newly provisioned user lands as
  `User`.
- Integration (`TrailBlaze.Repository.Test`): the migration applies the column and the `User`
  default to rows that predate it; seeding run twice against one SQL Server database yields one
  admin row with `Role = Admin`.
- Integration (`TrailBlaze.Api.Test`): the app booted twice against the same database reports
  exactly one admin row; a request whose token claims admin but whose row says `User` is treated as
  `User` by whatever reads the role.

## Notes / non-goals

- **No admin screens.** The role is elevated rights inside the same UI, not a separate surface
  (PRD Decision #21).
- **No role management API** — no endpoint promotes, demotes, lists, or deletes users. The single
  admin is a configuration fact, which is what keeps the privilege auditable.
- **No enforcement here.** This feature exposes the role; feature 09 is what denies a non-owner
  and what grants the admin override.
- Multiple admins are deliberately unsupported: the PRD says exactly one is seeded. Adding a second
  is a change to this decision, not a configuration tweak.
- The seeded admin's `EntraObjectId` must correspond to a real Entra object, or the seed row is
  simply never claimed by a sign-in; that is an operator concern, not something this feature
  validates against the graph.
