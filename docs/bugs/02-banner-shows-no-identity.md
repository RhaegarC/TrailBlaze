# 02 — The banner shows no avatar or display name after sign-in

Status: **Fixed** — awaiting the pull request · [00-bug-log.md](00-bug-log.md)
Reported: 2026-09-30 · Severity: **Normal** — the app works and the profile screen is the workaround
Component: `src/web` — `src/App.tsx`, `src/auth/store.ts`

## Symptom

Signing in with Entra ID succeeds and the app becomes a signed-in user, and the right side of the
top banner never shows who that is. It shows a letter — `A` for an administrator, `U` for anyone
else — beside the word `admin` or `user`.

## Cause — a mock the port never replaced

The banner's signed-in chip is the Figma export's own, and the export has no server to read a name
from, so it hardcodes both halves: `{authRole === "admin" ? "A" : "U"}` inside the circle, and
`{authRole}` — the *role*, capitalized — as the label. `src/web/src/App.tsx` carried it through
verbatim, which is what the seam between the two allows; what was missing is the wiring that
replaces it, and the sign-in button in the same header has exactly that (an `@integration:begin`
marker and a real `signIn()` call behind it).

The data was already there and already fetched. `GET /user/me` answers with `displayName` and
`avatarUrl`, `adopt()` calls it on every sign-in and on every reload, and it kept the role and the
object id while dropping the avatar — so the store held half of what the banner needed and the
banner read neither.

## Fix

- `src/auth/store.ts` — `AuthState` gains `avatarUrl`, and the profile the store already fetched is
  applied through a new exported `applyProfile()`. It is exported because the store is not the last
  writer: the profile screen saves a new avatar and name, and the banner reads the store, so that
  save has to land back in it or the banner shows the previous avatar until a reload.
- `src/auth/identity.ts` — `initialsOf()`, the two letters for a caller with no avatar (and `?` for
  one with no name yet).
- `src/App.tsx` — the chip renders the avatar when there is one and those initials when there is
  not, and the label is the display name. The role word remains only as the fallback, where a name
  has not arrived. Both edits sit inside `@integration:begin/end` markers, and the profile screen's
  save now calls `applyProfile()` — inside the marker that region already had.

## Verification

**There is no regression test, because `src/web` has no test tier** — the same position bug 01
records, and for the same reason: `src/web/package.json` carries no runner. The change is
`verification-only`:

- `npx tsc --noEmit` — clean.
- `npm run build` — clean.
- `python scripts/web-seam.py` — *Drift from the export stays inside the seam* passes for
  `src/web/src/App.tsx`, so the wiring is inside a marked region and a future re-export cannot
  silently take it back. The run has two **pre-existing** failures elsewhere, in
  `src/web/.gitignore:25` and `src/web/.figma/make/site.json:2`, which this change does not touch.
- Manual sign-in — **not yet run**; the account is the reporter's, and it is the only place the
  avatar and the name can be seen arriving.

## Not covered here

The avatar and the name come from `GET /user/me`, so the banner is only as correct as that call.
It currently rides an **id token**, because `src/config.ts` requests `scopes: []` and
`acquireToken()` returns `idToken` on the silent path and `accessToken` on the popup path — two
different tokens for one request. That is a defect of its own, with the API's `aud` validation as
the thing it depends on, and it is not this bug: the banner was never reading the profile at all.
