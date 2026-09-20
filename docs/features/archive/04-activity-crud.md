# 04 — Activity CRUD

Status: **Archived** — merged to `develop` in PR #15 · [00-mission-1-sprint.md](../00-mission-1-sprint.md)
Source: [PRD](../../PRD.md) — Decisions #10/#11/#12/#25/#26 + "API surface" and the `activities` data-model row.

> **Archiving this one does not mean the slice is closed.** It shipped the CRUD mechanics and the
> anonymous paged list; the payload a list item carries, the detail read and the admin branch of the
> visibility rule are [05](05-public-activity-list.md)'s, and who may mutate an entry is
> [09](../09-permission-enforcement.md)'s. `develop` is not deployable until 09 lands — the
> sequencing note in the sprint file states why.

## Summary

Create, read, update, and delete an activity, and read the paged list the journal shows. The field
set is fixed: `Title`, `Location`, `ActivityDate` (a **calendar date — no time, no timezone**),
optional `Description`, optional `CoverImageBlobPath`, and `Type` — the **visibility**
(`Public` | `Shared` | `Private`, Decision #26). `CreatedOn` is stamped server-side and is the
list's sort key, newest entry first. This feature stores `Type` and applies the **read** half of the
visibility rule to the list; **who may mutate an entry is feature [09](../09-permission-enforcement.md)'s**,
and the detail read, the cover URL and the enriched payload are
[05](05-public-activity-list.md)'s.

Storing `Type` here rather than in feature 09 is deliberate: it is a column on the activity, so
it belongs with the other columns. Feature 09 owns the *evaluation* of it on the mutation side.

## Story

As a signed-in user I want to log, revise, and remove an activity so that the journal reflects
where I went and when.

## Dependencies

- [02-entra-auth](02-entra-auth.md) (caller identity, for `CreatedByUserId` attribution)

## Acceptance criteria

- [x] `activities` matches the PRD data model. Its own columns are `Title`, `Location`,
      `ActivityDate` (`date`), `Description` (nullable), `Type` (`nvarchar(16)`), `CoverImageBlobPath`
      (nullable) and `CreatedByUserId` (the caller's id as a plain column — the model declares no
      foreign keys); `Id` and the remaining audit and soft-delete
      columns come from `EntityBase` (the PRD draws them once), so `IsDeleted` is present and the
      global query filter applies to this table
- [x] Routes exist for `POST /api/activity`, `GET /api/activity/{id}`,
      `PUT /api/activity/{id}`, and `DELETE /api/activity/{id}`
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

The paged list, which is the first anonymous read in the product:

- [x] `GET /api/activity` is reachable with **no** bearer token and returns a paging envelope —
      `Items` plus the `Page`, `PageSize` and `Total` actually applied, so a caller can see the clamp
      and page deterministically
- [x] `page` is a **zero-based** index defaulting to 0, and `pageSize` defaults to **10**. A
      `pageSize` above 100 is **clamped to 100** rather than rejected or honoured, so the endpoint can
      never return an unbounded set (Decision #23)
- [x] `pageSize` of 0 or less falls back to the default, a negative `page` is read as the first page,
      and a page past the end returns 200 with an empty item list rather than an error
- [x] Items are ordered by `CreatedOn` **descending**, tie-broken by `Id` descending, so the order is
      total and stable across pages (Decision #10, **changed 2026-09-19** — see below)
- [x] **Anonymous visibility:** with no token the items are exactly the `Public` entries.
      **Signed-in visibility:** `Public` + `Shared` + the caller's own `Private`, and never another
      user's `Private` (Decision #26)
- [x] The filter is applied **in the query**, not to a materialised page: a row the caller may not see
      cannot consume a page slot, and `Total` counts the filtered set rather than the table
- [x] The list withholds `CoverImageBlobPath` from a caller with no token — the first anonymous
      response in the product, and an anonymous caller may not be handed a blob path (Decision #30)

**The admin branch of the visibility rule is not implemented here, and cannot be.** It reads a role,
and `IUserContextService` carries none — a role has exactly one source, the `users` row, which is a
store read this predicate does not perform. Reading it is part of the permission work
[09](../09-permission-enforcement.md) owns; until then an admin pages what a user pages. Asserted as
such rather than left to be discovered.

**Ordering was changed on 2026-09-19, and the PRD changed with it.** Decision #10 originally made the
user-chosen `ActivityDate` the sort key with `CreatedOn` as the tiebreaker; this feature's review
settled on **`CreatedOn` descending**, tie-broken by `Id` descending, because `ActivityDate` is
client-supplied and backdating is accepted — an entry written today for last month lands mid-list and
shifts a page boundary when it does. The list is ordered by when an entry was *written*, not by when
it claims to have happened.

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
  [testing-and-tdd.md](../../testing-and-tdd.md) for why that is not the deleted in-memory fake.
- Model (`TrailBlaze.Repository.Test`) — offline, no container: the column lengths, the `date`
  column type, the `CK_Activities_Type` check constraint, the absence of any relationship on
  `CreatedByUserId`, and the soft-delete filter. These run on a machine with no Docker, which is
  when someone is editing the model and most needs them.
- Database (`TrailBlaze.Repository.Test`, `Category=Container`) — against SQL Edge: the row
  round-trips through `DatabaseRepository` and is re-read through a fresh scope; the migrated
  `ActivityDate` column is `date` in `INFORMATION_SCHEMA` rather than only in the model; the insert
  is stamped; an edit read-then-written keeps `CreatedOn` and `CreatedByUserId`; the engine refuses
  a `Type` outside the closed set; a deleted row leaves the read path and stays in the table.
- Api (`TrailBlaze.Api.Test`) — offline by design. All four protected routes answer **401** to an
  anonymous caller, which asserts the authorization rule and the route template together: a misspelled
  path would be a 404. The list route is asserted from the other side — `GET /api/activity` reaches
  the service, so the answer is the **500** the unreachable store produces, and neither the 401 of an
  authorized-only route nor the 404 of a route that does not exist. **The host has no authentication
  scheme unless `TenantId` and `Audience` are configured**, and in that state an `[Authorize]` route
  answers 500 — so these tests wire both.
- Paging and visibility (`TrailBlaze.Service.Test`) — offline: the defaults, the 100 clamp, the
  fallback for a non-positive size, the saturating skip on a page past the end, the reported total,
  the anonymous and signed-in predicates compiled and applied to rows, and the ordering. The
  predicate is compiled from the expression the service hands the store, so what is asserted is the
  rule itself rather than a string that names a column.
- Paging against the engine (`TrailBlaze.Repository.Test`, `Category=Container`) — the store's half:
  a page comes back newest first, paging returns every row exactly once, a past-the-end page is
  empty, a deleted row leaves both the page and the count, and **an excluded row does not consume a
  page slot** — the last seeded with the visible rows *older* than the hidden ones, so a filter
  applied after the page would show up as an empty page.
- The composed statement (`TrailBlaze.Repository.Test`, offline) — `ActivityModelTests` asserts the
  generated SQL carries the `ORDER BY`/`OFFSET` and that the `OFFSET` follows both the soft-delete
  predicate and `[Type]`, which is the model-level evidence for the same claim the container test
  makes about behaviour.
- **Not covered, and worth saying plainly: nothing exercises the service against a store.** The
  API tier's connection string points at a dead port, the database tier never builds a
  `TrailBlazeContext` (`TrailBlaze.Service.Test` does not reference `TrailBlaze.Repository`), and
  the API tier cannot authenticate without an Entra tenant, so no test drives
  `ActivityService` → `IDbRepository` → SQL Server end to end. The service's wiring is asserted
  against a recording double and the store's behaviour through direct repository calls; the seam
  *between* them is asserted by neither. That gap is closed in part: the request-decided claims run
  offline in this tier and the store outcomes run in the repository tier, with the seam the remaining
  cost.
- The calendar-date assertions compare the raw `date` value, not a `DateTime` with a `Kind`, so the
  test cannot pass or fail on the runner's local timezone.

## Notes / non-goals

- **This slice is not safe to deploy** — it builds the CRUD mechanics while every authenticated
  caller may still edit or delete anything, and feature **09** adds the ownership and admin rules.
  See the sequencing note in [00-mission-1-sprint.md](../00-mission-1-sprint.md).
- **The list ships here; what [05](05-public-activity-list.md) still owns is the payload.** The
  cover URL, the media count and the creator's display name are absent from a list item, the detail
  read `GET /api/activity/{id}` is still a plain 200 for any id, and the admin branch of the
  visibility rule is unread. No caller-selectable sort, no search, no filtering (Decision #23) — the
  visibility filter is an access rule rather than a query the caller chose.
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
- **`Type` is stored, not enforced, in this slice.** `GET /api/activity/{id}` here returns the
  row for any id; hiding a `Shared` or `Private` entry from a caller who may not read it is the
  read-filtering rule owned by [05-public-activity-list](05-public-activity-list.md) and
  [09-permission-enforcement](../09-permission-enforcement.md). Splitting it this way keeps the CRUD
  mechanics testable on their own, exactly as the ownership rules are split out.
