# 01 — Popup sign-in never completes

Status: **Archived** — merged to `develop` in PR #34 · [00-bug-log.md](../00-bug-log.md)
Reported: 2026-09-28 · Severity: **High** — a major function is broken and nothing is exposed
Component: `src/web` — `src/auth/store.ts`, `src/config.ts`

## Symptom

Signing in with Entra ID opens the popup and the account is chosen, and the popup is then left
standing on `http://localhost:8443/#code=…` and never closes. The app stays a visitor.

## Cause — two defects, in the same commit

1. **MSAL v5 hands a popup's response back over a `BroadcastChannel`, and the app never posts to
   it.** `PopupClient.waitForPopupResponse` listens on a channel named by the request's library
   state, for `popupBridgeTimeout` (60 s by default), and the only thing that posts to it is
   `broadcastResponseToMainFrame()` from `@azure/msal-browser/redirect-bridge`. That call belongs to
   the page the IdP landed on — this app, in the popup, at the redirect URI — and `initialise()`
   called `msal.initialize()` and read the account cache and nothing else. So the opener waited out
   its timeout and the popup stayed where the IdP had put it.
2. **The requested scope was empty.** `1aa7b02` replaced the derived `<clientId>/.default` with
   `scopes: []`. MSAL appends its OIDC defaults to an empty list, so the authorize request asked for
   `openid profile offline_access` alone — an id token and **no access token for the API**. The API
   validates `aud = <clientId>`, so no request could have carried a usable token even once the popup
   closed.

## Fix

- `src/auth/store.ts` — `initialise()` hands the response back and returns on the page that carries
  one (`hasAuthResponse()`, asked through MSAL's own URL parser, so an ordinary load is not mistaken
  for a response page). The bridge closes the popup and the opener's `loginPopup` resolves.
- `src/config.ts` — the scope is derived again: `<clientId>/.default`, or `VITE_ENTRA_SCOPE` when a
  deployment names something else.

## Verification

**There is no regression test, because `src/web` has no test tier.** The only hand-written frontend
work is API integration, verified end-to-end ([testing-and-tdd.md](../../testing-and-tdd.md)), and
`src/web/package.json` carries no runner — no vitest, no jsdom. Adding one is a toolchain decision,
not part of this fix, so the change is recorded as `verification-only`:

- `npx tsc --noEmit` — clean.
- `npm run build` — clean, and the bundle carries the bridge (`Microsoft Authentication` once,
  `redirect_bridge_timeout`), so `@azure/msal-browser/redirect-bridge` resolved and was not
  tree-shaken away.
- Manual sign-in — **not yet run**; the account is the reporter's, and it is what the fix has to be
  confirmed by.
