# 10 — Figma Integration

Status: **Archived** — merged to `develop` in PR #31 · [00-mission-1-sprint.md](../00-mission-1-sprint.md)
Source: [PRD](../../PRD.md) — Decisions #17/#22/#26–#30 + "Frontend build" + "System overview".

## Summary

The frontend integration. The Figma Make export lands in `src/web/` as a single role-gated React
19 (Vite + TypeScript + Tailwind) app; the screens themselves are authored in Figma Make and are
not hand-built. The only hand-written work is wiring that export to the backend: MSAL sign-in,
the API calls behind each screen, role gating of the controls, and rendering API responses —
including SAS-backed media — into the exported components. This feature turns ten backend
features into one usable product.

The export carries screens and controls the earlier flat, everything-is-public model did not have,
and they are part of what this feature wires: a **profile screen** (display name, bio, avatar,
theme, language), a **visibility selector** on the activity form, a **visibility badge** and
**media count** in the list and detail, and a detail view that groups an activity's media **by
uploader**. Wiring them is the same work as any other screen — the API slice behind each is
specified by the numbered backend features, not here.

Per [docs/testing-and-tdd.md](../../testing-and-tdd.md) the frontend is **out of TDD scope** — it is
not test-first — so the acceptance criteria below are **manual and observable**: each one is a
thing a person can verify by opening the app, signing in or not, and acting.

## Story

As a signed-in user I want the exported Figma app wired to the API so that I can browse
activities, sign in to see their media, post and edit my own entries, and — as an admin — manage
anyone's, through the interface the designer authored rather than a hand-built one.

## Dependencies

- [02-entra-auth](02-entra-auth.md) (the profile API — `PUT /user/me`, `POST`/`DELETE /user/me/avatar` — and the profile columns it writes, which the profile screen below consumes)
- [05-public-activity-list](05-public-activity-list.md) (paged, date-descending list with covers for anonymous callers)
- [07-sas-delivery](07-sas-delivery.md) (short-lived SAS URLs that make media renderable)
- [08-cover-images](08-cover-images.md) (cover upload, routed to `covers` or `media` by the activity's `Type`, and the cover URL — plain or SAS — in list/detail responses)
- [09-permission-enforcement](09-permission-enforcement.md) (the role rules the UI must reflect)

**Blocked until the Figma Make export exists.** Everything this feature depends on can be
finished and tested before the export arrives; the export is the gate (see
[00-mission-1-sprint.md](../00-mission-1-sprint.md) open items).

## Acceptance criteria

Verified by hand against a running stack; each is observable in a browser.

- [ ] The Figma Make export is committed under `src/web/` and builds and runs as a single React
      app (Vite + TypeScript + Tailwind) that talks to the API — no second frontend, no
      re-authored screens
- [ ] **Anonymous** (no sign-in): the activity list renders the **Public** entries only, date
      descending, with **cover images visible**, the **visibility badge**, the **media count**, and
      the **creator's display name** on each row, and **no media shown**; paging works and
      respects the server-clamped `pageSize`
- [ ] **Anonymous**: opening a `Public` activity shows its title, location, date, description,
      cover, media count, and creator display name, and **no image or video from the private
      container is present anywhere in the rendered page or its network requests**; and the
      response it renders carries no user id, blob path, or SAS URL (Decision #30)
- [ ] **Unreadable entries are not-found, not an error**: opening a `Shared` entry anonymously, or
      a `Private` entry that the signed-in caller does not own, puts the app in its **not-found**
      state because the API returns **404** — the app does not show a permission error, a blank
      page, or a crash
- [ ] Sign-in uses **MSAL against Entra ID**; a successful sign-in obtains a token that the app
      attaches as a bearer token on subsequent API calls, and a sign-out returns the app to the
      anonymous view
- [ ] **Signed in**: media on the activity detail renders — images display and videos play — from
      the **short-lived SAS URLs** returned by `GET /api/media/{id}/url`, not from a public blob
      URL
- [ ] A SAS URL is fetched only after sign-in; an anonymous session never issues the media-URL
      request
- [ ] **Create**: the create form collects title, location, activity date, optional description,
      an optional cover, and the **visibility selector** (`Public`/`Shared`/`Private`), and a
      successful submit posts to `POST /api/activity` (cover via
      `POST /api/activity/{id}/cover`) with the chosen `Type`; the new activity appears in the
      list with a badge matching what was chosen
- [ ] **Upload media**: an image and a video can be attached from the UI to **any activity the
      signed-in user can read** — their own or another user's (Decision #27) — and then appear in
      that activity's media after reload; the server's rejection of an oversize or disallowed file
      surfaces to the user rather than failing silently
- [ ] **Media grouped by uploader**: the detail view renders the activity's media in
      **per-uploader groups** that collapse and expand, each group labelled with the uploader's
      display name, and the grouping follows who added each item rather than the activity's owner
- [ ] **Delete respects collaboration**: the delete affordance appears on an item the signed-in
      user uploaded, and on every item for an `Admin` — and on **no** item otherwise, including the
      activity's owner's own entry (Decision #27, narrowed 2026-09-24); a deletion goes through
      `DELETE /api/media/{id}` and the item is gone on reload
- [ ] **Edit own**: an owned activity's edit form loads current values **including the visibility
      selector**, saves via `PUT /api/activity/{id}`, and the change is visible on reload; an
      edit that moves the activity across the public line leaves its cover rendering correctly
      from wherever it now lives (Decision #29)
- [ ] **Role gating**: edit and delete controls appear on the signed-in user's **own** activities
      and on **no other** activity for a `User`; for an `Admin` they appear on **every** activity
- [ ] **Affordances respect visibility**: on a `Private` activity its **owner** sees the edit and
      delete controls and an `Admin` does too, while a **non-owner never reaches the page at all**
      — the API's 404 puts the app in its not-found state, so the controls are not merely hidden
- [ ] **Profile screen**: `GET /user/me` populates the profile screen; `PUT /user/me` saves display
      name, bio, theme, and language, and the saved values are still there after a reload in a
      fresh session; the avatar uploads via `POST /user/me/avatar` into the **public** `avatars`
      container and renders from the plain URL that comes back, and `DELETE /user/me/avatar` clears
      it
- [ ] **Admin override**: signing in as the admin (the row whose `Role` reads `Admin`), another
      user's activity can be edited and deleted from the UI and the change persists
- [ ] A UI action the caller is not permitted to perform results in the API's 401/403 being shown
      as a refusal — not a silent no-op and not a crash — and the app does not present the action
      as having succeeded; a **404** on a read it may not make surfaces as not-found instead, and
      the app never treats the two as interchangeable (Decision #26)
- [ ] Cover images render in the public list for **anonymous** visitors, confirming the public
      container path end-to-end in the browser, and an avatar renders for an **anonymous** visitor
      from the public `avatars` container
- [ ] The app is driven entirely by configuration for the API base URL, Entra client/tenant IDs,
      and any scope names — no environment-specific values are hard-coded in the export

## Tests (TDD)

Not applicable by design. The frontend is **out of TDD scope** per
[docs/testing-and-tdd.md](../../testing-and-tdd.md) — this feature has no unit or integration test
tier to write against, because the screens come from Figma Make and the hand-written surface is
API integration, which is verified manually end-to-end. Nothing here adds to `dotnet test`.

What exists instead:

- The manual walkthrough in [11-e2e-verification](../11-e2e-verification.md) is the verification
  instrument for this feature; the criteria above are its per-screen detail.
- The backend it calls is fully covered by the existing tiers — no correctness claim about
  permissions, SAS expiry, or upload validation is being made here that 05/07/08/09 do not
  already test.

## Notes / non-goals

- No hand-authored screens, components, or styling: if a screen is wrong, it is fixed in Figma
  Make and re-exported, not patched in `src/web/`.
- No frontend test framework, no component tests, no Playwright/Cypress suite — this is the
  deliberate consequence of the TDD scope in [docs/testing-and-tdd.md](../../testing-and-tdd.md).
- No offline support and no native mobile app (PRD out-of-scope list).
- No admin screens (Decision #21): the admin role adds rights to the same controls and the same
  screens, nothing more.
- No search or filtering in the list (Decision #23) — pagination only.
- The app does not attempt to transcode or poster-thumbnail video (Decision #15); an HEVC `.mov`
  that the browser cannot decode will simply not play here, which is a known accepted limitation
  and not a defect in this integration.

### Where the line between wiring and authoring was drawn

The export renders some controls and leaves them inert, and it does not render others at all. The
two are different work, and conflating them is how a non-goal gets broken:

- **A control the export renders but leaves inert is wiring.** The "Upload cover image" button in
  `ActivityForm` rendered with no handler and no file input at all, so it is now a `<label>` that
  carries one. Its markup and styling are unchanged; the element had to become one that can hold a
  file input, which is the minimum that makes the label true.
- **A screen element the export does not render is authoring, and is not added here.** Where an
  acceptance criterion needs one, the criterion is recorded as unmet below rather than met by
  hand-written UI, because the non-goal above says a wrong screen is fixed in Figma Make and
  re-exported.

### Known gaps in the current export

Re-audited against the export as committed, file and line numbers included. Each is a defect in
the **Figma Make export**, not in the integration, and each is fixed in Figma Make and re-exported
rather than patched in `src/web/`. **Three acceptance criteria cannot be met until that re-export
lands**; they are named here so a green build is not mistaken for a met criterion.

- **Videos do not play** — criterion 6. `ActivityDetail` renders a video as a filename-and-size row
  with a "SAS required" badge and **no `<video>` element** ([`figma/src/App.tsx:759-772`](../../../figma/src/App.tsx#L759-L772)).
  Images display: the SAS URL `GET /api/media/{id}/url` returns is fetched per item and set as each
  image's `src`. For a video the same URL is fetched and sits unused in the item's `url`, so the
  data is wired and nothing renders it. The badge now reads a little untruthfully — the SAS URL it
  says is required has been obtained — and fixing that is part of the same re-export.
- **There is no per-media delete control** — criterion 11. The detail view's media grid gives each
  image exactly one action, opening the lightbox ([`figma/src/App.tsx:748-756`](../../../figma/src/App.tsx#L748-L756)),
  and the video row has none. The only `onDelete` in the export is the activity's
  ([`figma/src/App.tsx:684`](../../../figma/src/App.tsx#L684)). `DELETE /api/media/{id}` is therefore
  wired into the API client and **uncalled**, and the collaborator/admin delete rule — the whole
  point of the 2026-09-24 narrowing of Decision #27 — is unreachable from the UI.
- **No avatar renders for an anonymous visitor** — criterion 18. `avatarPreview` appears only
  inside `UserProfile` ([`figma/src/App.tsx:1222-1244`](../../../figma/src/App.tsx#L1222-L1244)), which
  is reachable only once signed in. The public `avatars` container path is exercised by the
  signed-in profile screen and by nothing an anonymous visitor sees; `creatorDisplayName` renders
  in the list and detail as **text**, never as an image.

Three defects the earlier draft of this section listed were **stale**, and are recorded here as
fixed so the next reader does not go looking: the top-banner `+` button does route to the upload
form on a detail page (`plusTarget`, [`figma/src/App.tsx:405-407`](../../../figma/src/App.tsx#L405-L407));
`ActivityDetail` is reached by that button, so it is not without an upload affordance; and the
button's `authRole !== "visitor"` gate is **correct** as committed, because Decision #27 lets any
signed-in caller contribute to an activity they can read.

One minor wording defect, noted rather than escalated: the visibility selector describes `Private`
as "Only you" ([`figma/src/App.tsx:114`](../../../figma/src/App.tsx#L114)), while feature 09 gives an
administrator read access to it too. The label understates who can see the entry.

### What this branch did and did not verify

The implementation is checked by `tsc --noEmit`, by `vite build`, and by
[`scripts/web-seam.py`](../../../scripts/web-seam.py), which asserts that every line of `src/web/`
differing from the export falls inside a marked seam. Against the local container stack
(`src/api/docker-compose.yml`), the anonymous list answers `{"items":[],"page":0,"pageSize":10,"total":0}`
— matching the declared `WireActivityPage` field-for-field — an unknown activity id answers 404, an
anonymous `POST` answers 401, and a CORS preflight from `http://localhost:8443` is allowed while an
unlisted origin gets no allow header.

**The browser walkthrough has not been run.** No browser is available on the machine this was
implemented on, so no criterion above is marked met on the strength of having seen it work. That
pass is [11-e2e-verification](../11-e2e-verification.md)'s, and it is the only thing that closes this
feature.
