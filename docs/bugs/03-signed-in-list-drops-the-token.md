# 03 — A signed-in caller's own `Shared` and `Private` activities are invisible

Status: **Open** — fix in `fix/03-signed-in-list-drops-the-token` · [00-bug-log.md](00-bug-log.md)
Reported: 2026-09-30 · Severity: **High** — a whole class of entries is unreachable, and nothing is
exposed
Component: `src/web` — `src/api/endpoints.ts`, `src/api/client.ts`, `src/data/hooks.ts`

## Symptom

Signed in, the list holds `Public` entries only. A `Shared` activity is absent from it, and so is
the caller's **own** `Private` one — including an entry created moments earlier in the same session,
which the create response returns and the next read then drops. The detail route agrees: the owner
of a `Private` activity is answered the 404 that an unreadable entry gets.

## Cause — the two activity reads ask for no credential

`src/api/endpoints.ts` passed `anonymous: true` on both activity reads, and `src/api/client.ts` reads
that flag as an instruction to skip the token entirely:

```ts
if (!anonymous) {
  const token = await tokenSource();
  if (token) headers.Authorization = `Bearer ${token}`;
}
```

So the request left without an `Authorization` header even with an account in MSAL's cache. The
server is not misbehaving: both routes carry `[AllowAnonymous]`, so no token is *required*, and
`UserContextService.EntraObjectId` correctly reads null because `context.User.Identity.IsAuthenticated`
is false. `ActivityAuthorizationService.ResolveAsync` therefore returns `Caller.Anonymous`, and
`VisibleTo` applies `activity => activity.Type == Constant.ActivityType.Public`. The filter answered
exactly what it was asked for, for the caller it was given.

### The reported cause does not hold

The hypothesis was that `UserContextService.AuthenticatedUser` is always null, even after sign-in. It
is not, and creation proves it: `ActivityService.CreateAsync` answers `NoCaller()` — a 401 — for a
caller it cannot identify, so an activity that exists at all is evidence that the identity resolved on
that request. The local stage database agrees: its `Users` table holds a row per account, and every
`Activities.CreatedBy` names one of them. `GET /user/me` reaches the same service and is how those
rows came to exist.

An identity that resolves on `POST /api/activity` cannot fail to resolve on `GET /api/activity` for
the same account in the same session. The difference was the header, not the service.

### A second cause — the caller was never part of the request

Correcting the header alone would not have been enough. `useActivities`, `useActivity` and
`useActivityMedia` each fetched on mount and then again only when their arguments changed —
`[page, pageSize]`, `[id]`, `[activityId]` — and who the caller is is not among them. Adoption is
asynchronous: `initialise()` awaits MSAL and then `GET /user/me` before the store reports
`signed-in`, while the fetch effect runs on mount. So for a signed-in reader **the first fetch of
every page load is the anonymous one, and nothing ever followed it**; the same is true of signing in
without a reload, which the app does through a popup and which leaves the mounted list holding the
answer it already had.

Both causes produce one symptom, and either alone is enough to produce it. They are fixed together
because a fix to the first leaves the report reproducible through the second.

### It is not a regression from feature 12

`git blame` dates both flags to `5eff3e4` — *feat(web): Wire the Figma export to the API*, feature 10,
2026-09-25. Every change since, feature 12 included, left them alone. The consequence is larger than
this report: **"signing in widens the list" has never been true in the app** — not since the export
was first wired to a server. The PRD's reading matrix and feature 11's criterion are what it
contradicts.

## Fix

- `src/api/endpoints.ts` — `listActivities` and `getActivity` stop passing the flag, so both send the
  caller's token when there is one, exactly as every other route already did. Nothing changes for a
  visitor: with no account there is no token to send, and the same anonymous filter runs.
- `src/api/client.ts` — the `anonymous` option is **deleted** rather than left with no callers. Both
  of its call sites were this bug, and its only effect is to suppress a credential the caller has —
  which is what turned a wiring mistake into a silent, quieter list instead of a loud failure.
- `src/data/hooks.ts` — `useActivities`, `useActivity` and `useActivityMedia` take the store's
  `status` as a dependency, so each refetches when a credential arrives or goes away. `status` rather
  than the user id: it changes exactly when the token does, which is once per sign-in and once per
  sign-out, where the id also moves as the profile lands.

## Verification

**There is no regression test, because `src/web` has no test tier** — no runner in
`src/web/package.json`, the same position bugs 01 and 02 record. The change is `verification-only`:

- `npx tsc --noEmit` — clean; removing the option would have failed here had anything else used it.
- `npm run build` — clean.
- `python scripts/web-seam.py` — unchanged from `develop`: the two pre-existing failures in
  `src/web/.gitignore:25` and `src/web/.figma/make/site.json:2`, and nothing new. Both edited files
  are hand-written rather than export-compared, so no seam marker applies.
- Manual, signed in — **not yet run.** The list now carrying `Shared` entries and the caller's own
  `Private` ones is the observable claim, and only a browser against a tenant can show it.

The server half is already asserted, and these tests were passing throughout:
`ActivityServiceTests.An_anonymous_page_reaches_public_entries_only` and
`A_signed_in_page_reaches_public_shared_and_its_own_private_entries` compile the predicate the
repository is handed and check it against both callers. What no tier covered is the header the
client sends — the seam between them is where this bug lived, and it is the seam
[testing-and-tdd.md](../testing-and-tdd.md) names rather than hides.

## Not covered here

- **Feature 11's walkthrough is what would have caught it**, and that pass has not been run. This fix
  makes its *"signing in widens the list"* criterion reachable in the app for the first time; it does
  not observe it.
- **The API cannot tell a deliberate anonymous request from a forgotten credential**, and this bug is
  not a reason to make it try. Both reads are public by design; the caller's token widens what a
  signed-in reader sees, and requiring one would take away the anonymous list.
