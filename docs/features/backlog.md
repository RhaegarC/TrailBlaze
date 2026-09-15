# Backlog

Ideas that are not yet features. The `/implement` and `/next` selection scans skip this file —
only numbered `docs/features/NN-name.md` files claim a priority slot. Promote an item by running
`/capture feature NN`, which stress-tests it into a real spec.

## Candidates

| Idea | Why it's not specced yet |
|---|---|
| **Search and filtering on the list** | Explicitly deferred in PRD Decision #23 — pagination only for v1. The obvious first addition once the core is solid. |
| **Video transcoding / poster thumbnails** | Deferred in PRD Decision #15. The concrete pain is that an iPhone's HEVC `.mov` won't play in Chrome or Firefox; transcoding is the fix, and it needs a background worker. |
| **Per-media visibility** | The public/private split is per *class*, not per item (Decisions #13/#14). A per-item flag would let a user publish one photo from an otherwise private activity. |
| **Comments or reactions** | No social layer in Mission 1. Would raise moderation questions the app currently has no answer for. |
| **Maps view of activities** | `Location` is free text by decision (Decision #12) — there are no coordinates to plot. Would require structured location first. |
| **Activity duration / distance / type** | Deliberately not in the field set (Decision #11). Would revive the "conditional fields" idea the inherited ladder had. |
| **Offline / mobile app** | Web-only for Mission 1. |

## Rejected (do not re-propose without new information)

| Idea | Why it was rejected |
|---|---|
| Local-disk media storage | Replaced by Azure Blob (Decision #4). |
| Azurite emulator | Declined — real Azure in every environment (Decision #5). |
| API-proxied media streaming | Chosen against; SAS URLs were picked so Azure carries the bandwidth and video seeking works (Decision #7). |
| Per-user private journals | Rejected — TrailBlaze is one shared feed (Decision #3). |
| Inherited dynamic-form features (conditional fields, remote lookups, form config with live preview) | Copied in from a different product; `Activity` has fixed fields and `Location` is free text, so these have nothing to describe (Decision #18). |
