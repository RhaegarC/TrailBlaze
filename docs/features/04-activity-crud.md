# 04 — Activity CRUD

Status: **In progress** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #10/#11/#12/#25/#26 + "API surface" and the `activities` data-model row.

## Summary

Create, read, update, and delete an activity. The field set is fixed: `Title`, `Location`,
`ActivityDate` (a **calendar date — no time, no timezone**), optional `Description`, optional
`CoverImageBlobPath`, and `Type` — the **visibility** (`Public` | `Shared` | `Private`, Decision
#26). `CreatedOn` is recorded for audit and doubles as the tiebreaker that orders two activities
logged on the same day. This feature ships the CRUD mechanics only; **who is allowed to call
them, and who is allowed to read the result, are features 09 and 05** — this feature stores and
round-trips `Type`, it does not enforce it.

Storing `Type` here rather than in feature 09 is deliberate: it is a column on the activity, so
it belongs with the other columns. Feature 09 owns the *evaluation* of it.

## Story

As a signed-in user I want to log, revise, and remove an activity so that the journal reflects
where I went and when.

## Dependencies

- [02-entra-auth](archive/02-entra-auth.md) (caller identity, for `CreatedByUserId` attribution)

## Acceptance criteria

- [x] `activities` matches the PRD data model. Its own columns are `Title`, `Location`,
      `ActivityDate` (`date`), `Description` (nullable), `Type` (`nvarchar(16)`), `CoverImageBlobPath`
      (nullable) and `CreatedByUserId` (the caller's id as a plain column — the model declares no
      foreign keys, [item 23](../tech-debt/23-foreign-keys-asserted-that-do-not-exist.md)); `Id` and the remaining audit and soft-delete
      columns come from `EntityBase` (the PRD draws them once), so `IsDeleted` is present and the
      global query filter applies to this table
- [x] Routes exist for `POST /api/activities`, `GET /api/activities/{id}`,
      `PUT /api/activities/{id}`, and `DELETE /api/activities/{id}`
- [x] `Type` accepts only `Public`, `Shared`, and `Private`; any other value is rejected with 400
      naming the field, and no row is written. Input comparison is case-insensitive but the stored
      value is canonical, so a reader never has to normalise it
- [x] `Type` is required on `POST` in the sense that it defaults to `Public` when omitted — an
      existing client that does not send the field keeps working, and the default is asserted
      rather than assumed
- [x] `PUT` may change `Type`; the new value is visible to the next read with no separate
      publish step. Crossing the public line is what triggers the cover move owned by feature
      [08-cover-images](08-cover-images.md), so this route must not block or mask that transition
- [x] `POST` sets `CreatedByUserId` from the caller's provisioned `users.Id` and ignores any
      `CreatedByUserId` supplied in the request body
- [x] `CreatedOn` is set server-side at insert and ignores any client-supplied value
- [x] `ActivityDate` round-trips as a calendar date: a request for `2026-03-14` stores and returns
      `2026-03-14` regardless of the server's or the client's timezone, with no midnight conversion
      and no off-by-one day (PRD Decision #25)
- [x] Backdating is accepted, and no date is silently clamped, defaulted, or rewritten to today
      (PRD Decision #10)
- [x] `Title` is required and non-blank, max 200 characters; `Location` is required and non-blank,
      max 200 characters and stored as free text with no lookup or structured parsing
      (PRD Decision #12)
- [x] `Description` is optional and accepts long text; an absent or whitespace-only value is stored
      as null rather than as an empty string
- [x] Validation failures return 400 identifying the offending field, and no row is written —
      a rejected create leaves the table unchanged
- [x] `GET` returns the activity's text and its cover path; an unknown id returns 404
- [x] `PUT` updates the mutable text fields and leaves `CreatedByUserId` and `CreatedOn`
      untouched; an unknown id returns 404
- [x] `PUT` leaves the stored `Type` alone when the body omits it, rather than defaulting it as
      `POST` does. A visibility change is a disclosure, so it has to be asked for rather than fall
      out of a field a client predating it never sent — the one place the two bodies differ
- [x] `DELETE` soft-deletes the row and returns 204; the row is retained with `IsDeleted` set and the
      global query filter hides it from every read, so deleting an already-deleted id returns 404
- [x] `CoverImageBlobPath` is never settable through the create or update body — it is written only
      by the cover upload path in feature 08

**Not this feature's criterion:** ordering by `ActivityDate DESC, CreatedOn DESC`. It was listed
here and belongs to [05-public-activity-list](05-public-activity-list.md), which owns the list, its
paging and its sort; a criterion written twice is one that can disagree with itself.

## Tests (TDD)

Every claim below is asserted, and the three tiers are named where they are asserted. **One claim
is not covered by any tier, and is called out rather than implied.**

- Unit (`TrailBlaze.Service.Test`) — offline, no store. The validation boundaries — blank and
  whitespace-only `Title`/`Location` rejected, 200 characters accepted and 201 rejected, a missing
  date rejected, every bad field reported in one response; `Description` normalised to null and the
  surrounding whitespace dropped; `Type` accepted case-insensitively, stored canonically, and
  defaulted when absent. Plus the request shape: **neither request type has a property for
  `CreatedByUserId`, `CreatedOn`, `Id`, `IsDeleted` or `CoverImageBlobPath`**, asserted by
  reflection, so "the service ignores a body-supplied creator" is a fact a reader can check rather
  than a check that could be deleted. Attribution itself is asserted against a purpose-built
  recording double for `IDbRepository` that answers no reads — see
  [testing-and-tdd.md](../testing-and-tdd.md) for why that is not the deleted in-memory fake.
- Model (`TrailBlaze.Repository.Test`) — offline, no container: the column lengths, the `date`
  column type, the `CK_Activities_Type` check constraint, the absence of any relationship on
  `CreatedByUserId`, and the soft-delete filter. These run on a machine with no Docker, which is
  when someone is editing the model and most needs them.
- Database (`TrailBlaze.Repository.Test`, `Category=Container`) — against SQL Edge: the row
  round-trips through `DatabaseRepository` and is re-read through a fresh scope; the migrated
  `ActivityDate` column is `date` in `INFORMATION_SCHEMA` rather than only in the model; the insert
  is stamped; an edit read-then-written keeps `CreatedOn` and `CreatedByUserId`; the engine refuses
  a `Type` outside the closed set; a deleted row leaves the read path and stays in the table.
- Api (`TrailBlaze.Api.Test`) — offline by design. All four routes answer **401** to an anonymous
  caller, which asserts the authorization rule and the route template together: a misspelled path
  would be a 404. **The host has no authentication scheme unless `TenantId` and `Audience` are
  configured**, and in that state an `[Authorize]` route answers 500 — see
  [item 28](../tech-debt/28-unconfigured-auth-answers-500.md) — so these tests wire both.
- **Not covered, and worth saying plainly: nothing exercises the service against a store.** The
  API tier's connection string points at a dead port, the database tier never builds a
  `TrailBlazeContext` (`TrailBlaze.Service.Test` does not reference `TrailBlaze.Repository`), and
  the API tier cannot authenticate without an Entra tenant, so no test drives
  `ActivityService` → `IDbRepository` → SQL Server end to end. The service's wiring is asserted
  against a recording double and the store's behaviour through direct repository calls; the seam
  *between* them is asserted by neither. This is the gap
  [item 25](../tech-debt/25-service-test-tier-is-empty.md) records, now closed in part: the
  request-decided claims run offline in this tier and the store outcomes run in the repository tier,
  with the seam the remaining cost.
- The calendar-date assertions compare the raw `date` value, not a `DateTime` with a `Kind`, so the
  test cannot pass or fail on the runner's local timezone.

## Notes / non-goals

- **This slice is not safe to deploy** — it builds the CRUD mechanics while every authenticated
  caller may still edit or delete anything, and feature **09** adds the ownership and admin rules.
  See the sequencing note in [00-mission-1-sprint.md](00-mission-1-sprint.md).
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
- **`Type` is stored, not enforced, in this slice.** `GET /api/activities/{id}` here returns the
  row for any id; hiding a `Shared` or `Private` entry from a caller who may not read it is the
  read-filtering rule owned by [05-public-activity-list](05-public-activity-list.md) and
  [09-permission-enforcement](09-permission-enforcement.md). Splitting it this way keeps the CRUD
  mechanics testable on their own, exactly as the ownership rules are split out.
