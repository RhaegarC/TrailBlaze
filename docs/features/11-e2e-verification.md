# 11 — E2E Verification

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #5/#8/#19/#26–#30 + "Deployment" + "Definition of Done" in [00-mission-1-sprint.md](00-mission-1-sprint.md).

## Summary

The closing verification feature. It adds no new functionality: it is a full-stack pass of the
whole product against a **running** stack — the API from `src/api/Dockerfile`, the **real** Azure
SQL Database, the **real** Azure Blob account, and the **real** Entra ID tenant — with the
Figma-integrated app in
front of it. Its job is to prove the pieces work *together* in the environment they will
actually run in, which no single feature's tests can show, and to walk the end-to-end journey
from anonymous browsing through admin override. The journey now includes the parts of the model
that only exist end to end: per-entry visibility as a read filter with a 404 on the entries a
caller may not see, collaborative media added by more than one user, the cover-move across the
public line, and the server-side profile screen. The sprint's Definition of Done items are this
feature's exit condition: when they all check, Mission 1 is done.

## Story

As the team delivering this journal I want the complete journey exercised against a deployed
stack with real Azure and real Entra so that "it works on my machine against the emulator"
is replaced by evidence that a visitor, a user, and an admin each see exactly what the PRD says.

## Dependencies

- [10-figma-integration](10-figma-integration.md) (the integrated app this pass drives)

This feature exercises the output of every feature before it — 01 through 10 — and cannot pass
unless they all pass. It is last in the ladder by design.

It also carries one thing that was planned elsewhere: **[09](09-permission-enforcement.md)'s
live-pipeline status matrix** — each endpoint driven over the real pipeline with a test token, and
the **403/404 pair asserted on the same route** *(2026-09-24 — moved here from 09, whose Api tier
boots against an unreachable connection string and would fail on the connection rather than on the
rule. Test-token infrastructure arrives with the tier that can use it, rather than as scaffolding
in 09.)* The criteria below already assert those statuses as part of the journey; this note is
here so a reader looking for the bullet in 09 finds where it went.

## Acceptance criteria

Each is performed against a running stack: the API running from its container image, configured
against the real Azure SQL Database, the real Azure Blob account and real Entra ID, with the
`src/web/` app served alongside. No fake `IStorageRepository`, no `InMemory` provider — and **no
emulator**. This pass is the tier that deliberately does not use one, and the distinction is the
whole of its value: a container on this machine cannot answer whether the real account's
containers exist, whether its signatures are accepted, whether its container access levels are what
the app assumes, or whether the real tenant issues the token the API validates.

- [ ] The API runs against the real Azure SQL Database; `GET /health` returns 200, and the schema
      matches the PRD data model — the migration set having been applied by the pipeline, not by
      the API
- [ ] The running API reaches the **real** `covers` and `avatars` (public) and `media` (private)
      containers, and the **real** Entra tenant, by configuration; no test double, emulator, or
      placeholder account string is anywhere in this path — the emulators the ordinary suite falls
      back to are the thing this criterion is distinguished *from*
- [ ] **Anonymous browsing — Public only**: with no sign-in, `GET /api/activity` returns a
      paged, date-descending list containing **Public** entries only — every `Shared` and
      `Private` entry is absent from it — and the app renders it with covers; `pageSize` is
      clamped server-side when a caller asks for more than the maximum
- [ ] **Anonymous detail**: `GET /api/activity/{id}` for a `Public` entry returns the activity's
      text, cover, media count, and creator display name, and the rendered page exposes **no**
      user id, blob path, SAS URL, or private-container byte (Decision #30)
- [ ] **Unreadable entries are 404, not 403**: the same detail request for a `Shared` or
      `Private` entry returns **404** to an anonymous caller — not 403, and not a redacted 200
- [ ] **Anonymous denial**: calling `GET /api/activity/{id}/media` and `GET /api/media/{id}/url`
      without a token returns **401** in both cases, and no blob operation is reached
- [ ] **Sign in**: authenticating through the app against Entra ID succeeds, the token is
      validated by the API, and the caller's `users` row is auto-provisioned on first sight of the
      `oid`
- [ ] **Signing in widens the list**: signed in, the list additionally contains the `Shared`
      entries and the caller's **own** `Private` ones; another user's `Private` entry is still
      absent from it
- [ ] **Private is owner-and-admin only**: a `Private` activity is absent from a second signed-in
      non-owner's list and its detail returns **404**, while the same URL renders for its owner
      and for the admin
- [ ] **Media visible**: signed in, the activity's images and videos render in the app from
      **short-lived SAS URLs**, and an expired SAS stops rendering while a freshly minted one
      works
- [ ] **Create**: a new activity posted from the app with each of the three visibility values
      appears in the list at the correct date-ordering position and is readable by exactly the
      callers its `Type` allows — a `Public` one visible to an anonymous session too
- [ ] **Upload**: an image, a video, and a cover image all upload successfully — the media landing
      in the **private** container and the cover in `covers` (public) because the activity is
      `Public` — and a file over the size cap or of a disallowed type is rejected by the server
      and surfaced in the UI
- [ ] **Cover-move round trip**: taking an activity that has a cover from `Public` to `Private`
      and saving it moves the cover — afterwards the **old public URL no longer serves the
      image**, while the cover still renders from a SAS URL — and editing it back to `Public`
      restores a plain public URL that needs no SAS
- [ ] **Collaborative media**: a second signed-in user adds media to the **first** user's `Public`
      activity and succeeds; the detail view groups the media **by uploader** with working
      collapse/expand; and the second user's item is deletable by that user, by the activity's
      owner, and by the admin
- [ ] **Collaboration refused where the caller cannot see the activity**: that same second user
      attempting to add media to a `Private` activity they cannot read is refused with **404**,
      not 403 — and `GET /api/activity/{id}/media` / `GET /api/media/{id}/url` return **404**
      for them too, so the media surface cannot be used to probe for the entry — while a
      signed-in caller who *can* read an activity but neither owns it nor uploaded a given item is
      refused deletion of that item with **403**
- [ ] **Edit own**: the creator edits their own activity from the app and the change persists
      across reload
- [ ] **Second user refused**: a second signed-in, non-admin user is refused with **403** when
      attempting to edit, delete, or replace the cover of the first user's activity, while still
      being able to read it and — if it is readable — add media to it (Decision #27)
- [ ] **Admin override**: the admin — the `users` row whose `Role` was set to `Admin` by hand, since
      nothing in the application grants it — signs in and successfully edits and deletes another
      user's activity, including one the admin is not the owner of and that is `Private`
- [ ] **Deletion**: deleting a single media item removes its row and blob, leaving the activity and
      its other media intact; deleting the **activity** removes neither — it is a soft delete, so
      its media rows, their blobs and its cover blob from whichever container holds it (`covers` or
      `media`) all survive, which is what a later restore depends on
- [ ] **Profile**: the profile screen saves display name, bio, theme, and language via
      `PUT /user/me` and the change persists across a reload; the avatar round-trips through
      `POST`/`DELETE /user/me/avatar` into the **public** `avatars` container, confirmed by a
      **credential-free HTTP GET** of the returned avatar URL returning the image — a public-read
      container is the whole point of putting an avatar there, and it is the one thing a signed-in
      check would hide
- [ ] **Theme and language are server-side**: switching theme or language, then reloading the app
      in a fresh session, renders the **stored** preference rather than the default — the settings
      are read back from the caller's `users` row, not held in component state
- [ ] **Definition of Done**: every checkbox in the
      [00-mission-1-sprint.md](00-mission-1-sprint.md) Definition of Done list is checked as a
      result of this pass — this is the exit condition for Mission 1
- [ ] **Concurrent first sign-in**: two simultaneous authenticated requests carrying the same,
      previously unseen `oid` are served without a 500. Provisioning is an unguarded
      read-then-insert ([02-entra-auth](archive/02-entra-auth.md#closed-in-this-pass-2026-09-16)), so the
      losing request fails its insert unless a retry is added — this is the criterion that decides
      whether one is needed, and it needs a real database and more than one replica, which is why
      it can only be settled here
- [ ] **Honest limits recorded**: the pass records what it did *not* prove (see Notes) rather than
      implying full coverage

## Tests (TDD)

No new functionality means no new feature tests — this feature does not add a RED → GREEN cycle.
What it runs is everything that already exists, plus the tiers that only this pass can reach:

- Unit / integration (`dotnet test` from `src/api/`): the full suite must be green on the same
  commit under verification. This is a precondition of the pass, not the pass itself.
- Storage integration (`TrailBlaze.Repository.Test`, tagged `Category=Container`): `dotnet test
  --filter "Category=Container"` with `TRAILBLAZE_STORAGE_CONNECTION` naming the **real** account —
  the tier runs the real `AzureBlobStorageRepository`, and what this pass adds over the ordinary run
  is the account behind it: same code, a real one instead of the emulator. It is the only tier that
  can catch SAS generation, content-type round-tripping, and the
  **cover-move** copy-then-delete across the public line (Decision #29), where the assertion that
  matters is that the old public blob is gone.
- Manual end-to-end walkthrough: the journey in the acceptance criteria above, performed by hand
  against the running stack, because the frontend it drives is out of TDD scope
  ([docs/testing-and-tdd.md](../testing-and-tdd.md)).

## Notes / non-goals

- **Not new functionality.** If this feature needs code written to pass, the defect belongs to the
  feature that owns it (01–10), not here.
- **It cannot run yet: the Figma export is mock-only.** The committed export under `src/web/` makes
  no API call, has no MSAL, hard-codes the role, and leaves its upload controls inert, so every
  criterion above that is phrased as *app* behaviour is blocked until
  [10-figma-integration](10-figma-integration.md) wires it up. The API-side criteria can be
  exercised in the meantime by calling the endpoints directly; that proves the API, not this pass,
  and does not close this feature.
- **The profile screen is new surface.** The display name, bio, avatar, theme, and language
  criteria above depend on the profile API (Decision #28) as well as on the frontend wiring; a
  failure there is a defect in the feature that owns that endpoint, not here.
- **What it cannot prove.** The pass does not establish performance, load behaviour, or
  concurrent-writer correctness; it is one scripted journey, not a soak test. It does not test a
  browser matrix — it is run in the browser(s) the team has, and a defect only visible elsewhere
  would escape it. It does not exercise the pipeline or the deployed ACA and Static Web Apps
  environments (Decision #19's `develop`/`master` flow is verified by the pipeline itself).
- **The HEVC/`.mov` gap is an accepted limitation, not a failure.** An iPhone's HEVC `.mov` is
  stored faithfully but will not play in Chrome or Firefox, because video is stored as-is with no
  transcoding and no thumbnails (Decision #15), and per-item visibility is out of scope too —
  visibility is set per *activity* (Decision #26), never per media item. If a video fails to
  render for this reason, the pass records it as the known gap and does not treat it as a defect —
  the criterion is that the upload, storage, SAS issuance, and delivery round-trip worked, which
  they did.
- **No new automated E2E harness.** This remains a documented manual pass over a running stack,
  consistent with the frontend being out of TDD scope; no Playwright/Cypress suite is introduced
  here.
- Not a release gate for the pipeline, and not a substitute for the per-feature tests that produced
  the green suite it starts from.
- **Database target.** The database is **Azure SQL Database** (PRD Decision #16); the provider swap
  landed in feature 01, and development and this pass run against a real Azure SQL Database rather
  than a local stand-in. The suite is the one place that has a stand-in — the container tier's
  `azure-sql-edge` (2026-09-18) — and it is a real SQL Server engine, which is precisely why "it
  passed against Edge" is not evidence about the target. This pass therefore runs against the real
  Azure SQL Database, and still records which server and database it actually ran against rather
  than assuming the target one.
