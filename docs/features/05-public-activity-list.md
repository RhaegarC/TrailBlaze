# 05 — Public Activity List

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #2/#3/#10/#23/#25 + "Media storage & delivery" + "API surface" + "Authentication & authorization".

## Summary

The anonymous read surface: a paged list of activities ordered by activity date descending, plus
an anonymous detail read. This is the only feature in the ladder that serves content to a caller
with no token — the two endpoints it covers are the deliberate exceptions to default-deny
(PRD "Authentication & authorization"). The anonymous payload carries **text and the cover image
URL only**; media is never reachable here, which is what makes the public/private split hold at
the edge of the product.

## Story

As a visitor I want to page through activities ordered by date descending so that I can browse the
journal without signing in.

## Dependencies

- [04-activity-crud](04-activity-crud.md) (activities exist, with `Title`, `Location`, `ActivityDate`, optional `Description`)
- [08-cover-images](08-cover-images.md) (populates `CoverImageBlobPath`; the list renders the cover URL this feature projects)

## Acceptance criteria

- [ ] `GET /api/activities` returns 200 for a request carrying **no** bearer token
- [ ] `GET /api/activities/{id}` returns 200 for a request carrying **no** bearer token
- [ ] Query parameters are `page` (1-based, default 1) and `pageSize` (default 20)
- [ ] `pageSize` above 100 is **clamped to 100**, not rejected and not honoured — the endpoint can never return an unbounded set
- [ ] `pageSize` of 0, negative, or non-numeric falls back to the default 20 rather than erroring
- [ ] Items are ordered by `ActivityDate` descending; rows sharing an `ActivityDate` are tie-broken by `CreatedOn` descending, so the order is total and stable across pages (Decision #10)
- [ ] The response is a paging envelope — `items` plus the total count and the page/pageSize actually applied, so a caller can detect the clamp and page deterministically
- [ ] Each list item exposes exactly `Id`, `Title`, `Location`, `ActivityDate`, `Description`, and the cover image URL — no media id, blob path, SAS URL, media count, or any other media-derived field appears anywhere in the anonymous payload
- [ ] The anonymous payload is asserted by serializing the response model and checking the property set, not by eyeballing a sample response
- [ ] `ActivityDate` serializes as a calendar date with no time component and no timezone offset — no UTC-midnight conversion (Decision #25)
- [ ] The anonymous detail response exposes the same field set as a list item
- [ ] An unknown activity id returns **404**, never 401 and never 500
- [ ] A page beyond the last returns 200 with an empty item list
- [ ] The list query does not join or read the `media` table at all
- [ ] An activity whose `CoverImageBlobPath` is null is returned with a null/absent cover URL rather than being filtered out or erroring

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`): pagination clamping across the boundary — `pageSize` of 0, 1, 20, 100, 101, 1000, and garbage input; page numbering at first/last/past-end page; ordering with same-date rows resolving on `CreatedOn`. **Hot spot (security / information disclosure):** the projection for the anonymous response carries no media field, and the service never queries the `media` table — RED first, because this is the boundary that keeps private media private.
- Integration (`TrailBlaze.Repository.Test`): the ordering + paging query runs offline — the generated
  SQL (the skip/take paging plus the `ActivityDate DESC, CreatedOn DESC` ordering, and the
  soft-delete filter) is inspected with `ToQueryString()`, including a same-`ActivityDate` case whose
  tie-break must appear in the expected total order ([testing-and-tdd.md](../testing-and-tdd.md)).
- Integration (`TrailBlaze.Api.Test`): an end-to-end anonymous request with no `Authorization` header returning 200 and valid JSON; the serialized JSON object for a list item containing no media key; `ActivityDate` emitting as `yyyy-MM-dd`.

## Notes / non-goals

- **No search and no filtering** of any kind — pagination only, for v1 (PRD Decision #23). Also no caller-selectable sort; date descending is the only order.
- **No media, ever, in the anonymous payload** (PRD Decision #2). The signed-in media surfaces are features 06 (metadata) and 07 (SAS URL); they are separate endpoints and this feature must not grow a flag that adds media to the public response.
- The feed is **one shared journal** — the list is not scoped, filtered, or hidden per caller (PRD Decision #3). There is no "my activities" variant here.
- This feature does not enforce ownership or any per-caller rule; anonymous read is granted deliberately on exactly these two endpoints. Feature [09-permission-enforcement](09-permission-enforcement.md) states the full permission matrix and the denials everywhere else.
- No caching layer, ETag, or conditional-request handling.
- No cover resizing, no CDN, no image proxy — the cover URL is passed through as stored by feature 08.
