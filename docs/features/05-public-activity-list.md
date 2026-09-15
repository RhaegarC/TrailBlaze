# 05 — Public Activity List

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #2/#3/#10/#23/#25/#26/#29/#30 + "Media storage & delivery" + "API surface" + "Authentication & authorization".

## Summary

The read surface: a paged list of activities ordered by activity date descending, plus a detail
read. These two endpoints are the only routes in the API that a caller with **no** token may
reach — the deliberate exceptions to default-deny (PRD "Authentication & authorization") — so
this feature also owns the rule deciding **which activities a given caller sees at all**.

That rule is `activities.Type` (Decision #26). An anonymous caller pages the `Public` entries; a
signed-in caller pages `Public` + `Shared` + their own `Private`; an admin pages everything. The
list applies it as a filter; the detail read applies it as a **404**, so an entry a caller may not
read is indistinguishable from one that does not exist — "this row exists" is itself the fact
being withheld.

Media is never reachable here at any visibility level. The payload names the creator and gives an
item count (Decision #30) but carries no user id, no blob path, and no SAS URL, which is what
keeps the private container's contents out of a response an anonymous caller can obtain.

## Story

As a visitor I want to page through activities ordered by date descending so that I can browse the
journal without signing in.

## Dependencies

- [04-activity-crud](04-activity-crud.md) (activities exist, with `Title`, `Location`, `ActivityDate`, optional `Description`)
- [08-cover-images](08-cover-images.md) (populates `CoverImageBlobPath`; the list renders the cover URL this feature projects)

## Acceptance criteria

- [ ] `GET /api/activities` returns 200 for a request carrying **no** bearer token
- [ ] `GET /api/activities/{id}` returns 200 for a request carrying **no** bearer token, **for a `Public` entry**
- [ ] Query parameters are `page` (1-based, default 1) and `pageSize` (default 20)
- [ ] `pageSize` above 100 is **clamped to 100**, not rejected and not honoured — the endpoint can never return an unbounded set
- [ ] `pageSize` of 0, negative, or non-numeric falls back to the default 20 rather than erroring
- [ ] Items are ordered by `ActivityDate` descending; rows sharing an `ActivityDate` are tie-broken by `CreatedOn` descending, so the order is total and stable across pages (Decision #10)
- [ ] The response is a paging envelope — `items` plus the total count and the page/pageSize actually applied, so a caller can detect the clamp and page deterministically
- [ ] **Anonymous visibility filter:** with no bearer token the items are exactly the `Public` activities — no `Shared` and no `Private` row appears on any page, and the reported total counts the filtered set rather than the table (Decision #26)
- [ ] **Signed-in visibility filter:** a `User` sees `Public` + `Shared` + their own `Private` and **no other user's `Private`**; an `Admin` sees everything (Decision #26)
- [ ] Visibility is applied **in the query**, not by discarding rows from a materialised page. A filtered-out row must not consume a page slot — otherwise a page of 20 can return fewer than 20 visible items while more exist beyond it
- [ ] **Detail visibility:** `GET /api/activities/{id}` returns 200 for a caller who may read the entry and **404 for one who may not** — a `Shared` entry to an anonymous caller, a `Private` entry to anyone but its owner and admins. 404 rather than 403, because existence itself is withheld (PRD "Authentication & authorization")
- [ ] Each list item exposes the activity's own fields — `Id`, `Title`, `Location`, `ActivityDate`, `Description`, `Type` — plus the cover image URL, the **media count**, and the **creator's display name** (Decision #30). `CreatedByUserId` is the one further field, and only for an authenticated caller (next criterion)
- [ ] **No user id ever appears in an anonymous response** — not `CreatedByUserId`, not an uploader id. The creator is named by display name only; under the current key shape a user id *is* the Entra object id (PRD "Current state vs. target", Decision #30)
- [ ] The anonymous payload carries no media id, blob path, SAS URL, content type, size, or file name. The **count is the only media-derived value permitted**
- [ ] Authenticated responses **may** carry `CreatedByUserId`, because the client needs it to decide whether to show edit controls; anonymous responses may not. Both shapes are asserted, so the difference is deliberate and reviewed rather than incidental
- [ ] The payload is asserted by serializing the response model and checking the property set, not by eyeballing a sample response
- [ ] `ActivityDate` serializes as a calendar date with no time component and no timezone offset — no UTC-midnight conversion (Decision #25)
- [ ] `Type` is present on every item as one of `Public` / `Shared` / `Private`, so a client renders the badge without a second request
- [ ] The detail response exposes the same field set as a list item
- [ ] An unknown activity id returns **404**, never 401 and never 500
- [ ] A page beyond the last returns 200 with an empty item list
- [ ] **The media count is gated by the same visibility rule as the entry itself** — it is a read of the media table, so a caller who may not read the activity never receives its count. It is a permitted disclosure, not a separate one
- [ ] An activity whose `CoverImageBlobPath` is null is returned with a null/absent cover URL rather than being filtered out or erroring
- [ ] The cover URL is a **plain public blob URL for a `Public` activity**, and a **short-lived SAS URL for a `Shared` or `Private`** one — whose cover lives in the private container (Decision #29). A test asserts which container each shape is derived from

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`): pagination clamping across the boundary — `pageSize` of 0, 1, 20, 100, 101, 1000, and garbage input; page numbering at first/last/past-end page; ordering with same-date rows resolving on `CreatedOn`. **Hot spot (security / access control — must be test-first):** the visibility predicate as an allow/deny table over {anonymous, signed-in non-owner, owner, admin} × {`Public`, `Shared`, `Private`}, asserting that an anonymous caller never receives a `Shared` or `Private` row and that a signed-in caller never receives another user's `Private` row. RED first: this is the boundary that keeps non-public entries out of an unauthenticated response.
- Unit (`TrailBlaze.Service.Test`) — **hot spot (information disclosure):** serialize the anonymous response model and assert the property set. No user id, no blob path, no SAS URL, no per-item media field; the count and the creator's display name **are** present. Asserted as a property set rather than a sample string so a later field addition fails the test instead of slipping through.
- Integration (`TrailBlaze.Repository.Test`): the ordering + paging **+ visibility filter** query runs offline — the generated SQL (the skip/take paging, the `ActivityDate DESC, CreatedOn DESC` ordering, the soft-delete filter, and the `Type` predicate) is inspected with `ToQueryString()`, including a same-`ActivityDate` case whose tie-break must appear in the expected total order. Assert the `Type` predicate is in the SQL **before** the skip/take, which is what proves filtered rows cannot consume page slots ([testing-and-tdd.md](../testing-and-tdd.md)).
- Integration (`TrailBlaze.Api.Test`): an end-to-end anonymous request with no `Authorization` header returning 200 and valid JSON; the same request returning no `Shared` or `Private` entry; a `Private` entry's detail returning 404 anonymously and under a second user's token, and 200 under its owner's; the serialized list item carrying no user id; `ActivityDate` emitting as `yyyy-MM-dd`.

## Notes / non-goals

- **No search and no content filtering** — pagination only, for v1 (PRD Decision #23). The visibility
  filter is not "filtering" in that sense: it is an access rule, not a caller-chosen query. There is
  still no caller-selectable sort; date descending is the only order.
- **No media bytes, ever, in this payload** (PRD Decision #2). The **count** is metadata and is now
  permitted (Decision #30). The signed-in media surfaces are features 06 (metadata) and 07 (SAS URL);
  they are separate endpoints, and this feature must not grow a flag that inlines media into the
  response.
- The feed is **one shared journal, scoped by visibility**: the list is filtered per caller on
  `activities.Type` (Decision #26). There is still no "my activities" variant — a caller's own
  entries appear in the same feed, not a separate one.
- **This feature does apply the read half of the permission rule, and should no longer be described
  as enforcing nothing.** What it does not do is mutation-side authorization: ownership of
  edit/delete, and the retrofitting of the other routes, belong to
  [09-permission-enforcement](09-permission-enforcement.md), which owns the full matrix. The
  visibility predicate lives in one shared place so the two features cannot drift apart.
- **This feature mints cover SAS URLs for non-public entries**, making it a second producer of SAS
  URLs alongside feature 07, which owns the media one. Stated plainly because it is a real widening
  of the SAS surface: the alternatives — omitting the cover for `Shared`/`Private` entries, or
  fetching it in a second round trip — were rejected because they leave the list visually
  inconsistent for exactly the callers entitled to see those entries.
- No caching layer, ETag, or conditional-request handling.
- No cover resizing, no CDN, no image proxy — the cover URL is passed through as stored by feature 08.
