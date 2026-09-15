# 04 — Activity CRUD

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #10/#11/#12/#25 + "API surface" and the `activities` data-model row.

## Summary

Create, read, update, and delete an activity. The field set is fixed: `Title`, `Location`,
`ActivityDate` (a **calendar date — no time, no timezone**), optional `Description`, and optional
`CoverImageBlobPath`. `CreatedOn` is recorded for audit and doubles as the tiebreaker that orders
two activities logged on the same day. This feature ships the CRUD mechanics only; who is allowed
to call them is feature 09.

## Story

As a signed-in user I want to log, revise, and remove an activity so that the journal reflects
where I went and when.

## Dependencies

- [02-entra-auth](02-entra-auth.md) (caller identity, for `CreatedByUserId` attribution)

## Acceptance criteria

- [ ] `activities` matches the PRD data model. Its own columns are `Title`, `Location`,
      `ActivityDate` (`date`), `Description` (nullable), `CoverImageBlobPath` (nullable) and
      `CreatedByUserId` FK → `users.Id`; `Id` and the remaining audit and soft-delete columns come
      from `EntityBase` (the PRD draws them once), so `IsDeleted` is present and the global query
      filter applies to this table
- [ ] Routes exist for `POST /api/activities`, `GET /api/activities/{id}`,
      `PUT /api/activities/{id}`, and `DELETE /api/activities/{id}`
- [ ] `POST` sets `CreatedByUserId` from the caller's provisioned `users.Id` and ignores any
      `CreatedByUserId` supplied in the request body
- [ ] `CreatedOn` is set server-side at insert and ignores any client-supplied value
- [ ] `ActivityDate` round-trips as a calendar date: a request for `2026-03-14` stores and returns
      `2026-03-14` regardless of the server's or the client's timezone, with no midnight conversion
      and no off-by-one day (PRD Decision #25)
- [ ] Backdating is accepted, and no date is silently clamped, defaulted, or rewritten to today
      (PRD Decision #10)
- [ ] `Title` is required and non-blank, max 200 characters; `Location` is required and non-blank,
      max 200 characters and stored as free text with no lookup or structured parsing
      (PRD Decision #12)
- [ ] `Description` is optional and accepts long text; an absent or whitespace-only value is stored
      as null rather than as an empty string
- [ ] Validation failures return 400 identifying the offending field, and no row is written —
      a rejected create leaves the table unchanged
- [ ] `GET` returns the activity's text and its cover path; an unknown id returns 404
- [ ] `PUT` updates the mutable text fields and leaves `CreatedByUserId` and `CreatedOn`
      untouched; an unknown id returns 404
- [ ] `DELETE` soft-deletes the row and returns 204; the row is retained with `IsDeleted` set and the
      global query filter hides it from every read, so deleting an already-deleted id returns 404
- [ ] `CoverImageBlobPath` is never settable through the create or update body — it is written only
      by the cover upload path in feature 08
- [ ] Ordering by `ActivityDate DESC, CreatedOn DESC` places the more recently created of two
      same-date activities first

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) — **hot spot**: validation boundaries — blank and
  whitespace-only `Title`/`Location` rejected, 200 characters accepted and 201 rejected, an empty
  `Description` normalised to null, an invalid calendar date rejected. Also that `CreatedByUserId`
  is taken from the caller even when the payload supplies a different id, and that `CreatedOn` is
  server-assigned rather than echoed from the request.
- Integration (`TrailBlaze.Repository.Test`): the query is exercised through the `DbContext` with no
  database — the save interception stamps `CreatedOn` before any connection opens, the `ActivityDate`
  mapping to a time-less `date` column and the FK to `users.Id` are asserted from the EF model, and
  the same-day ordering is inspected with `ToQueryString()`. The cascade to media rows is a mapping
  assertion (exercised properly once 06 exists) ([testing-and-tdd.md](../testing-and-tdd.md)).
- Integration (`TrailBlaze.Api.Test`): create → read → update → delete round-trip; 404 for unknown
  ids on read/update/delete; 400 with field detail on an invalid payload and the table unchanged
  afterwards.
- The calendar-date assertions must compare the raw `date` value, not a `DateTime` with a `Kind`,
  so the test cannot pass or fail on the runner's local timezone.

## Notes / non-goals

- **This slice is not safe to deploy.** Features 04–08 build the CRUD and media mechanics while
  every authenticated caller may still edit or delete anything. Feature **09** imposes the
  ownership and admin rules; until it merges, `develop` is not a usable environment
  (see the sequencing note in [00-mission-1-sprint.md](00-mission-1-sprint.md)). This is a
  build-order choice — the mechanics stay independently testable and 09 states the whole permission
  matrix as its own criteria — not an oversight.
- No `GET /api/activities` list, paging, or sorting endpoint — that is 05, which clamps `pageSize`
  server-side.
- No cover upload: the column exists, nothing but feature 08 writes it.
- No media upload or SAS delivery — 06 and 07.
- No search or filtering of any kind (PRD Decision #23).
- No optimistic concurrency token and no `If-Match`; concurrent edits are last-write-wins.
- No **user-facing** edit history screen and no versioning: there is no revision view and no restore.
  Soft delete and an audit trail *do* exist as platform conventions, not as mechanics this feature
  ships — `EntityBase` carries `IsDeleted` behind a global query filter, and an append-only `AuditLog`
  table is written by an `AuditSaveChangesInterceptor`. Neither is configured or exposed here.
- The anonymous/user/admin access matrix for these four routes is stated and enforced in 09, not
  here.
