# 05 — Public Activity List

Status: **Archived** — merged to `develop` in PR #19 · [00-mission-1-sprint.md](../00-mission-1-sprint.md)
Source: [PRD](../../PRD.md) — Decisions #2/#3/#10/#23/#25/#26/#29/#30 + "Media storage & delivery" + "API surface" + "Authentication & authorization".

> **Archiving this one does not mean the slice is closed.** It shipped the payload and the detail
> read; the **admin branch of the visibility rule** is still open, and cannot be otherwise — it reads
> a role, and no layer exposes one, so an administrator reads what an ordinary user reads until
> [09](../09-permission-enforcement.md) lands. `develop` is not deployable until it does — the
> sequencing note in the sprint file states why.

## Summary

The read surface's second half: the detail read, and the payload both reads carry.

**The list route, its paging and its visibility filter ship in [04](04-activity-crud.md)**, because a
list with no filter hands every caller every row — the paging and the rule deciding who sees what are
one mechanism, and were built as one. What is left here is the second read and the response shape:
`GET /api/activity/{id}`, which applies the same rule as a **404** rather than a filter, so an entry a
caller may not read is indistinguishable from one that does not exist — "this row exists" is itself
the fact being withheld; the cover URL, the media count and the creator's display name a list item
still does not carry; and the scoping that keeps a user id and a blob path out of a response anyone
can fetch.

**The detail read is the one route with no envelope and no paging, and that is the whole difference
between the two reads.** The list's `page`, `pageSize` and ordering belong to 04; this feature must
not change them, and restating them here would be the second copy that drifts.

One branch of the rule is not implementable yet: **an admin reads everything**, and reading a role
means reading the `users` row, which `IUserContextService` does not do. That is the permission work
[09](../09-permission-enforcement.md) owns, and until it lands an admin reads what a user reads.

Media is never reachable here at any visibility level. The payload names the creator and gives an
item count (Decision #30) but carries no user id, no blob path, and no SAS URL, which is what
keeps the private container's contents out of a response an anonymous caller can obtain.

## Story

As a reader I want an entry's own page and the name of the person who wrote it, so that the journal
reads as entries rather than as anonymous rows.

## Dependencies

- [04-activity-crud](04-activity-crud.md) (activities exist, with `Title`, `Location`, `ActivityDate`, optional `Description`)
- [08-cover-images](08-cover-images.md) (populates `CoverImageBlobPath`; the list renders the cover URL this feature projects)

## Acceptance criteria

- [x] `GET /api/activity/{id}` returns 200 for a request carrying **no** bearer token, **for a `Public` entry**
- [x] **Detail visibility:** `GET /api/activity/{id}` returns 200 for a caller who may read the entry and **404 for one who may not** — a `Shared` entry to an anonymous caller, a `Private` entry to anyone but its owner and admins. 404 rather than 403, because existence itself is withheld (PRD "Authentication & authorization")
- [ ] **Admin visibility:** an `Admin` reads everything, including entries no other caller may see (Decision #26). **Blocked, not skipped:** the branch reads a role, and no layer exposes one today — `IUserContextService` carries none, and a role's only source is the `users` row. It therefore lands with [09](../09-permission-enforcement.md)'s permission work, and until then this criterion stays open and an admin reads what a user reads
- [x] Each list item exposes the activity's own fields — `Id`, `Title`, `Location`, `ActivityDate`, `Description`, `Type` — plus the cover image URL, the **media count**, and the **creator's display name** (Decision #30). `CreatedByUserId` is the one further field, and only for an authenticated caller (next criterion)
- [x] **No user id ever appears in an anonymous response** — not `CreatedByUserId`, not an uploader id. The creator is named by display name only; under the current key shape a user id *is* the Entra object id (PRD "Current state vs. target", Decision #30)
- [x] The anonymous payload carries no media id, blob path, SAS URL, content type, size, or file name. The **count is the only media-derived value permitted**
- [x] Authenticated responses **may** carry `CreatedByUserId`, because the client needs it to decide whether to show edit controls; anonymous responses may not. Both shapes are asserted, so the difference is deliberate and reviewed rather than incidental
- [x] The payload is asserted by serializing the response model and checking the property set, not by eyeballing a sample response
- [x] `ActivityDate` serializes as a calendar date with no time component and no timezone offset — no UTC-midnight conversion (Decision #25)
- [x] `Type` is present on every item as one of `Public` / `Shared` / `Private`, so a client renders the badge without a second request
- [x] The detail response exposes the same field set as a list item
- [x] An unknown activity id returns **404**, never 401 and never 500
- [x] **The media count is gated by the same visibility rule as the entry itself** — it is a read of the media table, so a caller who may not read the activity never receives its count. It is a permitted disclosure, not a separate one
- [x] An activity whose `CoverImageBlobPath` is null is returned with a null/absent cover URL rather than being filtered out or erroring
- [x] The cover URL is a **plain public blob URL for a `Public` activity**, and a **short-lived SAS URL for a `Shared` or `Private`** one — whose cover lives in the private container (Decision #29). A test asserts which container each shape is derived from

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) — **hot spot (information disclosure — must be test-first):**
  serialize the anonymous response model and assert the property set. No user id, no blob path, no SAS
  URL, no per-item media field; the count and the creator's display name **are** present, and
  `CreatedByUserId` is present for a signed-in caller. Asserted as a property set rather than a sample
  string so a later field addition fails the test instead of slipping through. RED first: this is the
  boundary that keeps a user id out of a response anyone can fetch.
- Unit (`TrailBlaze.Service.Test`) — the detail read's visibility: a `Public` entry to a caller with no
  token, a `Shared` one to a signed-in caller, a `Private` one to its owner, and **404** for each case
  where the caller may not read it.
- Integration (`TrailBlaze.Api.Test`) — the route predicate, and the rest is unreachable **here**.
  The detail route admits an anonymous request: it reaches the service, so the answer is the **500**
  the factory's unreachable store produces, not the 401 of an authorized-only route nor the 404 of a
  route that does not exist. **The 200 and the 404 the criteria above name are not asserted in this
  tier**, and cannot be: it has no database to hold a `Public` entry and no Entra tenant to
  authenticate a second user against. Those are asserted offline in `TrailBlaze.Service.Test`, which
  drives the same visibility rule with a caller in hand. What is lost with the seam is that
  `200`/`404` was never observed travelling over HTTP — the status is asserted from the outcome the
  service returns, and the mapping from outcome to status by
  `ActivityRouteTests`' protected-route table and 04's list assertion.

**Paging, ordering and the visibility filter are asserted in [04](04-activity-crud.md)'s tiers and are
not restated here** — they already run, and a second copy of a test is a second thing to keep green.
What this feature adds to the tiers is the payload's shape and the detail read's status code.

## Notes / non-goals

- **No search and no content filtering** — pagination only, for v1 (PRD Decision #23). The visibility
  filter is not "filtering" in that sense: it is an access rule, not a caller-chosen query. There is
  still no caller-selectable sort; newest entry first is the only order, and it is 04's.
- **No media bytes, ever, in this payload** (PRD Decision #2). The **count** is metadata and is now
  permitted (Decision #30). The signed-in media surfaces are features 06 (metadata) and 07 (SAS URL);
  they are separate endpoints, and this feature must not grow a flag that inlines media into the
  response.
- The feed is **one shared journal, scoped by visibility**: the list is filtered per caller on
  `activities.Type` (Decision #26). There is still no "my activities" variant — a caller's own
  entries appear in the same feed, not a separate one.
- **The read half of the permission rule is in the code, and 04 shipped it with the list**; what this
  feature adds to it is the 404-because-existence-is-withheld answer on the detail read. The admin
  branch is still open, because it needs a role. Mutation-side authorization — ownership of
  edit/delete, and the retrofitting of the other routes — belongs to
  [09-permission-enforcement](../09-permission-enforcement.md), which owns the full matrix. The
  visibility predicate lives in one shared place so the features cannot drift apart.
- **This feature mints cover SAS URLs for non-public entries**, making it a second producer of SAS
  URLs alongside feature 07, which owns the media one. Stated plainly because it is a real widening
  of the SAS surface: the alternatives — omitting the cover for `Shared`/`Private` entries, or
  fetching it in a second round trip — were rejected because they leave the list visually
  inconsistent for exactly the callers entitled to see those entries.
- No caching layer, ETag, or conditional-request handling.
- No cover resizing, no CDN, no image proxy — the cover URL is passed through as stored by feature 08.
