# 11 — E2E Verification

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #5/#8/#19 + "Deployment (stage 1)" + "Definition of Done" in [00-mission-1-sprint.md](00-mission-1-sprint.md).

## Summary

The closing verification feature. It adds no new functionality: it is a full-stack pass of the
whole product against a **running** stack — `docker-compose` (API + Azure SQL Server), the
**real** Azure Blob account, and the **real** Entra ID tenant — with the Figma-integrated app in
front of it. Its job is to prove the pieces work *together* in the environment they will
actually run in, which no single feature's tests can show, and to walk the end-to-end journey
from anonymous browsing through admin override. The sprint's Definition of Done items are this
feature's exit condition: when they all check, Mission 1 is done.

## Story

As the team delivering this journal I want the complete journey exercised against a deployed
stack with real Azure and real Entra so that "it works on my machine with a fake storage service"
is replaced by evidence that a visitor, a user, and an admin each see exactly what the PRD says.

## Dependencies

- [10-figma-integration](10-figma-integration.md) (the integrated app this pass drives)

This feature exercises the output of every feature before it — 01 through 10 — and cannot pass
unless they all pass. It is last in the ladder by design.

## Acceptance criteria

Each is performed against a running stack: `docker-compose up` bringing up the API and Azure SQL
Server, configured against the real Azure Blob account and real Entra ID, with the `src/web/` app
served alongside. No fake `IStorageService`, no emulator, no `InMemory` provider.

- [ ] `docker-compose up` brings the API and Azure SQL Server up together; `GET /health` returns
      200, and EF migrations have been applied at startup with the schema the PRD data model describes
- [ ] The running API reaches the **real** `covers` (public) and `media` (private) containers and
      the **real** Entra tenant by configuration; the fake used in unit tests is nowhere in this
      path
- [ ] **Anonymous browsing**: with no sign-in, `GET /api/activities` returns a paged,
      date-descending list and the app renders it with covers; `pageSize` is clamped server-side
      when a caller asks for more than the maximum
- [ ] **Anonymous detail**: `GET /api/activities/{id}` returns the activity's text and cover, and
      the rendered page exposes **no** private-container bytes
- [ ] **Anonymous denial**: calling `GET /api/activities/{id}/media` and `GET /api/media/{id}/url`
      without a token returns **401** in both cases, and no blob operation is reached
- [ ] **Sign in**: authenticating through the app against Entra ID succeeds, the token is
      validated by the API, and the caller's `users` row is auto-provisioned on first sight of the
      `oid`
- [ ] **Media visible**: signed in, the activity's images and videos render in the app from
      **short-lived SAS URLs**, and an expired SAS stops rendering while a freshly minted one
      works
- [ ] **Create**: a new activity posted from the app appears in the public list (visible to an
      anonymous session too) with the correct date ordering position
- [ ] **Upload**: an image, a video, and a cover image all upload successfully — the media landing
      in the **private** container and the cover in the **public** one — and a file over the size
      cap or of a disallowed type is rejected by the server and surfaced in the UI
- [ ] **Edit own**: the creator edits their own activity from the app and the change persists
      across reload
- [ ] **Second user refused**: a second signed-in, non-admin user is refused with **403** when
      attempting to edit, delete, upload media to, or replace the cover of the first user's
      activity — and cannot obtain its media-URL endpoint's output for a mutation — while still
      being able to read the public list and detail
- [ ] **Admin override**: the seeded admin, signed in, successfully edits and deletes another
      user's activity
- [ ] **Delete cascades**: deleting an activity removes its media rows **and** the underlying blobs
      from the private container; deleting a single media item removes its row and blob, leaving
      the activity and its other media intact
- [ ] **Definition of Done**: every checkbox in the
      [00-mission-1-sprint.md](00-mission-1-sprint.md) Definition of Done list is checked as a
      result of this pass — this is the exit condition for Mission 1
- [ ] **Honest limits recorded**: the pass records what it did *not* prove (see Notes) rather than
      implying full coverage

## Tests (TDD)

No new functionality means no new feature tests — this feature does not add a RED → GREEN cycle.
What it runs is everything that already exists, plus the tiers that only this pass can reach:

- Unit / integration (`dotnet test` from `src/api/`): the full suite must be green on the same
  commit under verification. This is a precondition of the pass, not the pass itself.
- Storage integration (`TrailBlaze.Service.Test`, tagged): `dotnet test --filter
  Category=StorageIntegration` runs against the real Azure account — the tier that proves the blob
  implementation rather than the fake, and the only tier that can catch SAS generation and
  content-type round-tripping defects.
- Manual end-to-end walkthrough: the journey in the acceptance criteria above, performed by hand
  against the running stack, because the frontend it drives is out of TDD scope
  ([docs/testing-and-tdd.md](../testing-and-tdd.md)).

## Notes / non-goals

- **Not new functionality.** If this feature needs code written to pass, the defect belongs to the
  feature that owns it (01–10), not here.
- **What it cannot prove.** The pass does not establish performance, load behaviour, or
  concurrent-writer correctness; it is one scripted journey, not a soak test. It does not test a
  browser matrix — it is run in the browser(s) the team has, and a defect only visible elsewhere
  would escape it. It does not exercise the pipeline or any deployment beyond local Docker
  (Decision #19's `develop`/`master` flow is verified by the pipeline itself).
- **The HEVC/`.mov` gap is an accepted limitation, not a failure.** An iPhone's HEVC `.mov` is
  stored faithfully but will not play in Chrome or Firefox, because video is stored as-is with no
  transcoding and no thumbnails (Decision #15) and per-media visibility and transcoding are
  explicitly out of scope. If a video fails to render for this reason, the pass records it as the
  known gap and does not treat it as a defect — the criterion is that the upload, storage, SAS
  issuance, and delivery round-trip worked, which they did.
- **No new automated E2E harness.** This remains a documented manual pass over a running stack,
  consistent with the frontend being out of TDD scope; no Playwright/Cypress suite is introduced
  here.
- Not a release gate for the pipeline, and not a substitute for the per-feature tests that produced
  the green suite it starts from.
- **Database target vs. current code.** The target database is **Azure SQL Server** (PRD Decision #16),
  but the code today runs on PostgreSQL/Npgsql and that provider swap is still pending — so the pass
  records which provider it actually ran against rather than assuming the target one.
