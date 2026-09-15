# Backlog

Ideas that are not yet features. The `/implement` and `/next` selection scans skip this file —
only numbered `docs/features/NN-name.md` files claim a priority slot. Promote an item by running
`/capture feature NN`, which stress-tests it into a real spec.

## Candidates

| Idea | Why it's not specced yet |
|---|---|
| **Search and filtering on the list** | Explicitly deferred in PRD Decision #23 — pagination only for v1. The obvious first addition once the core is solid. |
| **Video transcoding / poster thumbnails** | Deferred in PRD Decision #15. The concrete pain is that an iPhone's HEVC `.mov` won't play in Chrome or Firefox; transcoding is the fix, and it needs a background worker. |
| **Per-media visibility** | Still out of scope: visibility is set **per activity** ([PRD](../PRD.md) Decision #26), never per item (Decision #14), so there is no "make this one photo public" flag — every item inherits its activity's visibility. A per-item flag would let a user publish one photo from an otherwise private activity. |
| **Comments or reactions** | No social layer in Mission 1. Would raise moderation questions the app currently has no answer for. |
| **Maps view of activities** | `Location` is free text by decision (Decision #12) — there are no coordinates to plot. Would require structured location first. |
| **Activity duration / distance / sport type** | Deliberately not in the field set (Decision #11) — a **sport/kind** field, which is a different thing from `activities.Type`, the visibility column ([PRD](../PRD.md) Decision #26). Would revive the "conditional fields" idea the inherited ladder had. |
| **Offline / mobile app** | Web-only for Mission 1. |

## Rejected (do not re-propose without new information)

| Idea | Why it was rejected |
|---|---|
| Local-disk media storage | Replaced by Azure Blob (Decision #4). |
| Azurite emulator | Declined — real Azure in every environment (Decision #5). |
| API-proxied media streaming | Chosen against; SAS URLs were picked so Azure carries the bandwidth and video seeking works (Decision #7). |
| Inherited dynamic-form features (conditional fields, remote lookups, form config with live preview) | Copied in from a different product; `Activity` has fixed fields and `Location` is free text, so these have nothing to describe (Decision #18). |

**Superseded.** **Per-user private journals** were rejected here under Decision #3, on the reading
that "private" meant a journal per user. That reading was wrong and
[PRD](../PRD.md) Decision #26 reverses the rejection: private **entries** now exist inside the one
shared feed. Decision #3 itself is unchanged — visibility narrows who may read an entry, it does
not partition the product into separate journals and adds no per-user feed. So the distinction to
keep in mind is private *entries* within one feed (adopted) versus a separate per-user journal
(still rejected).
