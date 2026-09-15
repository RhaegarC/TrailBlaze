# 10 — Figma Integration

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #17/#22/#26–#30 + "Frontend build" + "System overview".

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

Per [docs/testing-and-tdd.md](../testing-and-tdd.md) the frontend is **out of TDD scope** — it is
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
[00-mission-1-sprint.md](00-mission-1-sprint.md) open items).

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
      successful submit posts to `POST /api/activities` (cover via
      `POST /api/activities/{id}/cover`) with the chosen `Type`; the new activity appears in the
      list with a badge matching what was chosen
- [ ] **Upload media**: an image and a video can be attached from the UI to **any activity the
      signed-in user can read** — their own or another user's (Decision #27) — and then appear in
      that activity's media after reload; the server's rejection of an oversize or disallowed file
      surfaces to the user rather than failing silently
- [ ] **Media grouped by uploader**: the detail view renders the activity's media in
      **per-uploader groups** that collapse and expand, each group labelled with the uploader's
      display name, and the grouping follows who added each item rather than the activity's owner
- [ ] **Delete respects collaboration**: the delete affordance appears on an item the signed-in
      user uploaded, and on every item for the activity's owner and for an `Admin`; a deletion
      goes through `DELETE /api/media/{id}` and the item is gone on reload
- [ ] **Edit own**: an owned activity's edit form loads current values **including the visibility
      selector**, saves via `PUT /api/activities/{id}`, and the change is visible on reload; an
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
- [ ] **Admin override**: signing in as the seeded admin, another user's activity can be edited
      and deleted from the UI and the change persists
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
[docs/testing-and-tdd.md](../testing-and-tdd.md) — this feature has no unit or integration test
tier to write against, because the screens come from Figma Make and the hand-written surface is
API integration, which is verified manually end-to-end. Nothing here adds to `dotnet test`.

What exists instead:

- The manual walkthrough in [11-e2e-verification](11-e2e-verification.md) is the verification
  instrument for this feature; the criteria above are its per-screen detail.
- The backend it calls is fully covered by the existing tiers — no correctness claim about
  permissions, SAS expiry, or upload validation is being made here that 05/07/08/09 do not
  already test.

## Notes / non-goals

- No hand-authored screens, components, or styling: if a screen is wrong, it is fixed in Figma
  Make and re-exported, not patched in `src/web/`.
- No frontend test framework, no component tests, no Playwright/Cypress suite — this is the
  deliberate consequence of the TDD scope in [docs/testing-and-tdd.md](../testing-and-tdd.md).
- No offline support and no native mobile app (PRD out-of-scope list).
- No admin screens (Decision #21): the admin role adds rights to the same controls and the same
  screens, nothing more.
- No search or filtering in the list (Decision #23) — pagination only.
- The app does not attempt to transcode or poster-thumbnail video (Decision #15); an HEVC `.mov`
  that the browser cannot decode will simply not play here, which is a known accepted limitation
  and not a defect in this integration.

### Known gaps in the current export

Three concrete defects found by reading the committed export (`src/web/src/App.tsx`). Each is a
defect in the **Figma Make export**, not in the integration: per the non-goal above they are fixed
in Figma Make and re-exported, **not patched in `src/web/`**. Until that re-export lands, the
create/upload/collaboration criteria above cannot be met by the export as committed.

- **The top-banner `+` button is labelled for media, but wired to create** —
  `src/web/src/App.tsx:419-429`. On a detail page it relabels itself "Upload media" (`plusLabel`,
  `src/web/src/App.tsx:402`), yet **both branches of its click handler navigate to
  `{ name: "create" }`** (`src/web/src/App.tsx:421`), so it opens the *create-activity* form with
  no activity context. The media-upload entry point is labelled but not wired, and it silently
  takes the user somewhere else.
- **`ActivityDetail` has no upload affordance at all** — the component (`src/web/src/App.tsx:586`)
  and its media section (`src/web/src/App.tsx:700-775`). The detail view renders media, groups it
  by uploader with collapse/expand, and opens a lightbox, but nothing lets a signed-in reader add
  an item. The export therefore has **no working way to attach media to an existing activity**,
  which is the whole point of Decision #27.
- **The `+` button renders for any non-visitor** — `src/web/src/App.tsx:419`. The only gate is
  `authRole !== "visitor"`, so it also appears on another user's activity, where the upload it
  offers may be refused. It needs visibility/ownership gating: on an activity the caller cannot
  read the app is at a 404 anyway, and on one they can read but do not own the control should
  either offer the collaborative upload (Decision #27) or not appear.
