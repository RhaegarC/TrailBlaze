# 15 — The web app cannot call the API, and cannot acquire a token

Status: **open** · Kind: capability · Impact: blocks · Area: Web
Discharges via: **feature 10** · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

`src/web/` is a Figma Make export of the intended UI, rendered entirely from hardcoded data. It has
**no HTTP client and no auth library at all** —
[package.json](../../src/web/package.json) declares only `react` and `react-dom`. There is no MSAL,
no `@azure/msal-*`, no axios, no ky.

Grepping `src/web/src` for `fetch(`, `axios`, `msal`, `@azure`, `/api/`, `/activities` returns
**nothing** (2026-09-17). The app is not a client that happens to be pointed at mock data; it is
structurally incapable of making a request, and no dependency exists that could be configured to fix
that.

The concrete symptoms, all in [App.tsx](../../src/web/src/App.tsx):

| What | Where |
|---|---|
| `MOCK_ACTIVITIES` / `MOCK_MEDIA` are the entire data layer | [:223-307](../../src/web/src/App.tsx#L223) |
| `const [authRole] = useState<AuthRole>("user")` — **no setter**, so the role is permanently `"user"` | [:1184](../../src/web/src/App.tsx#L1184) |
| `currentUserId` hardcoded (`user-1` / `admin-1`) | [:1189](../../src/web/src/App.tsx#L1189) |
| Create and profile-save fake success with `setTimeout` (1400 ms / 2500 ms); delete just changes view | [:860](../../src/web/src/App.tsx#L860), [:1050](../../src/web/src/App.tsx#L1050), [:1211](../../src/web/src/App.tsx#L1211) |
| Sign-in is a bare `<a href="/auth/login">`, not MSAL | [:432](../../src/web/src/App.tsx#L432) |
| The banner `+` handler is a dead ternary — both branches are `{ name: "create" }` | [:419-429](../../src/web/src/App.tsx#L419-L429) |
| That `+` is labelled "Upload media" on a detail view while wired to create | [:402](../../src/web/src/App.tsx#L402) |
| `ActivityDetail` renders and groups media but has no control to add one | [:700-775](../../src/web/src/App.tsx#L700-L775) |

## Why it matters

**Every role-gated affordance in the export is decorative.** The line that matters most is `authRole`
having no setter: it is `"user"` forever, so `isAdmin` is permanently false and no admin surface can
ever be exercised — in the mock *or* after integration, if the same shape survives. A reviewer
looking at the export sees a working role gate; the gate is a constant.

**The dead ternary is worth reading twice.** `isDetail ? { name: "create" } : { name: "create" }` is
not a bug that survives scrutiny — it is the visible remnant of a behaviour that was *intended* and
never written: inside an activity, `+` should start a **media upload**, not open the create form. The
label at line 402 still says so. So the export records an intent its code does not implement, and
that intent is not obvious from either half alone.

**This gap currently lives in three places** — the sprint file's Open items, feature 10's "Known
gaps in the current export", and PR conversation. That is the same disease §12 had, and the reason
this item exists rather than being left to feature 10: one row, one link, and the other two reduce to
a pointer.

**It is `Discharges via: feature 10`, not orphaned work.** Feature 10 is exactly this — wiring the
export to the API. This item is recorded so the gap has a home and a status between now and then, not
to claim a second repair.

## Evidence

Checked against the code 2026-09-17.

- `package.json` dependency list: `react`, `react-dom` only.
- The greps above, all empty.
- [10-figma-integration.md](../features/10-figma-integration.md)'s "Known gaps in the current export"
  records the `+` defects independently, and
  [00-mission-1-sprint.md](../features/00-mission-1-sprint.md)'s Open items calls the export "a mock"
  that "issues no `fetch` and no MSAL call".
- The two Figma export defects are **export bugs, not patching targets**: the project's position is
  that they are fixed in Figma Make and re-exported rather than hand-patched in `src/web/`. Whatever
  fixes them, the fix is not a local edit that the next export overwrites.

## Testability

**verification-only** until feature 10 lands. There is no frontend test framework — a deliberate
TDD-scope decision — and no behaviour here that an assertion could hold, since the "behaviour" is the
absence of a client.

Feature 10 is where this becomes testable, and this item should not be closed by a test written
against mock data. Its close condition is that the app makes a real call, which feature 10's own
acceptance criteria already cover.

## Repair plan

**Not to be repaired under this item.** The repair is feature 10. What this item owes:

1. **Reduce the other two records to pointers.** The sprint file's Open items and feature 10's "Known
   gaps" should link here rather than restate the defects, so there is one description with one
   status. This is the work this item actually asks for now.
2. **Record the intent the dead ternary encodes** (media upload from within an activity) in feature
   10's scope, since it is currently visible only as a label that disagrees with its handler. If the
   re-export does not implement it, feature 10 should say so rather than let it be rediscovered.
3. **Flag the `authRole` shape to feature 10** as a thing not to carry over: a `useState` whose setter
   was discarded is easy to reproduce faithfully when wiring, and it would make the real gate
   untestable in exactly the same way.

## Out of scope / related

- **Feature 10 owns the fix** ([10-figma-integration.md](../features/10-figma-integration.md)).
  `debt-fix` must not start on this item — the `Discharges via` gate applies, and this is precisely
  the case it was written for.
- **Item [04](04-usercontroller-route-convention.md)** settles the route casing feature 10 will
  consume. Doing 04 first is worth more than doing it later, because feature 10 writes those paths.
- **The Figma export defects are re-export fixes**, so the "Known gaps" list in feature 10 stays a
  list of things to fix in Figma Make — this item aggregates them, it does not move them.

## Close checklist

- [ ] The sprint file's Open items and feature 10's Known gaps link here instead of restating
- [ ] The `+`-in-detail intent recorded in feature 10's scope, or explicitly dropped
- [ ] The `authRole` no-setter shape flagged to feature 10 as not to reproduce
- [ ] Closed by feature 10's PR — real API call observed, not asserted against mock data
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
