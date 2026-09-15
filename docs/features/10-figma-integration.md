# 10 — Figma Integration

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #17/#22 + "Frontend build" + "System overview".

## Summary

The frontend integration. The Figma Make export lands in `src/web/` as a single role-gated React
19 (Vite + TypeScript + Tailwind) app; the screens themselves are authored in Figma Make and are
not hand-built. The only hand-written work is wiring that export to the backend: MSAL sign-in,
the API calls behind each screen, role gating of the controls, and rendering API responses —
including SAS-backed media — into the exported components. This feature turns ten backend
features into one usable product.

Per [docs/testing-and-tdd.md](../testing-and-tdd.md) the frontend is **out of TDD scope** — it is
not test-first — so the acceptance criteria below are **manual and observable**: each one is a
thing a person can verify by opening the app, signing in or not, and acting.

## Story

As a signed-in user I want the exported Figma app wired to the API so that I can browse
activities, sign in to see their media, post and edit my own entries, and — as an admin — manage
anyone's, through the interface the designer authored rather than a hand-built one.

## Dependencies

- [05-public-activity-list](05-public-activity-list.md) (paged, date-descending list with covers for anonymous callers)
- [07-sas-delivery](07-sas-delivery.md) (short-lived SAS URLs that make media renderable)
- [08-cover-images](08-cover-images.md) (public cover upload + URL in list/detail responses)
- [09-permission-enforcement](09-permission-enforcement.md) (the role rules the UI must reflect)

**Blocked until the Figma Make export exists.** Everything this feature depends on can be
finished and tested before the export arrives; the export is the gate (see
[00-mission-1-sprint.md](00-mission-1-sprint.md) open items).

## Acceptance criteria

Verified by hand against a running stack; each is observable in a browser.

- [ ] The Figma Make export is committed under `src/web/` and builds and runs as a single React
      app (Vite + TypeScript + Tailwind) that talks to the API — no second frontend, no
      re-authored screens
- [ ] **Anonymous** (no sign-in): the activity list renders, date descending, with **cover images
      visible** and **no media shown**; paging works and respects the server-clamped `pageSize`
- [ ] **Anonymous**: opening an activity shows its title, location, date, and description, and
      **no image or video from the private container is present anywhere in the rendered page or
      its network requests**
- [ ] Sign-in uses **MSAL against Entra ID**; a successful sign-in obtains a token that the app
      attaches as a bearer token on subsequent API calls, and a sign-out returns the app to the
      anonymous view
- [ ] **Signed in**: media on the activity detail renders — images display and videos play — from
      the **short-lived SAS URLs** returned by `GET /api/media/{id}/url`, not from a public blob
      URL
- [ ] A SAS URL is fetched only after sign-in; an anonymous session never issues the media-URL
      request
- [ ] **Create**: the create form collects title, location, activity date, optional description,
      and an optional cover, and a successful submit posts to `POST /api/activities` (cover via
      `POST /api/activities/{id}/cover`) and the new activity appears in the list
- [ ] **Upload media**: an image and a video can be attached to an owned activity from the UI and
      then appear in that activity's media after reload; the server's rejection of an oversize or
      disallowed file surfaces to the user rather than failing silently
- [ ] **Edit own**: an owned activity's edit form loads current values, saves via
      `PUT /api/activities/{id}`, and the change is visible on reload
- [ ] **Role gating**: edit and delete controls appear on the signed-in user's **own** activities
      and on **no other** activity for a `User`; for an `Admin` they appear on **every** activity
- [ ] **Admin override**: signing in as the seeded admin, another user's activity can be edited
      and deleted from the UI and the change persists
- [ ] A UI action the caller is not permitted to perform results in the API's 401/403 being shown
      as a refusal — not a silent no-op and not a crash — and the app does not present the action
      as having succeeded
- [ ] Cover images render in the public list for **anonymous** visitors, confirming the public
      container path end-to-end in the browser
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
