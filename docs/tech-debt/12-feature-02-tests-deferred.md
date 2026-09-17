# 12 — Feature 02's behaviour shipped without the tests §10 requires

Status: **open** · Kind: test-gap · Impact: blocks · Area: Tests
Source: STANDARD §12.12 · Discharges via: 11 (in part) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

Feature 02 (Entra auth and the self-service profile slice) is implemented, merged to `develop` in
PR #4, and archived. Its tests were deliberately not written. By STANDARD §10 — *a behaviour change
without a test is not finished* — the feature is not finished, and its archived status records the
end of the document's lifecycle rather than a completed feature.

The two criteria that matter most were left explicitly unticked in the feature file, and both are
silent when wrong:

- **The privilege-escalation guard has no test.** `UpdateProfileRequest` has no `Role`, `Email` or
  `Id` property, and that absence is the *entire* control. Nothing would fail if a later change
  added one.
- **The avatar's public-read behaviour has no test**, so Decision #28's design intent is implemented
  but never fetched from the real container.

Recorded at [02-entra-auth.md#testing-status](../features/archive/02-entra-auth.md#testing-status).

## Why it matters

**An untested guard that everyone believes is tested is worse than one known to be untested**, which
is the feature file's own reasoning and is right. `Role` is not writable through the profile route
because a *property is absent from a DTO* — a shape no compiler, no schema, and no existing test
observes. The failure mode is a user promoting themselves to admin, silently and permanently, which
also defeats feature 03's seeding entirely.

**It is a live baseline, not a historical gap.** `develop` carries this code. Features 03 and
following build on the `users` table and the profile slice, so their tests will assume behaviour that
nothing has proven.

**The feature is the demonstration of the problem this register exists for.** Its own file says an
untested guard everyone believes is tested is worse than one known to be untested *because the second
one gets written*. That argument is why this item is a blocking test-gap rather than a note.

## Evidence

Checked against the feature file and the suite 2026-09-17.

- The suite is 42 tests, 41 passing, 1 skipped (the tagged storage tier without credentials). None of
  them covers feature 02's profile routes, the upload validator, or the provisioning conflict branch.
  Verified by running `dotnet test` 2026-09-17, which is also what settled the count —
  [testing-and-tdd.md](../testing-and-tdd.md) had it right while this file was off by one.
- [02-entra-auth.md](../features/archive/02-entra-auth.md) carries a `## Testing status` section, two
  unmet `[ ]` criteria, and a `## Tests (TDD)` section prefaced as unwritten — it is the plan for
  this item, not a description of coverage.
- The feature's own `Tests (TDD)` block names seven tiers' worth of tests. The sub-table below
  separates what is fixable now from what is not.

### What can be written now versus what cannot

The three items that route to feature 11 must not swallow the rest — most of this is ordinary
offline-testable work, and it should be done without waiting for 11.

| Assertion | Tier | Now? |
|---|---|---|
| `UpdateProfileRequest` carries no `Role`/`Email`/`Id` — by reflection | Service | **Yes** |
| A `PUT /user/me` body attempting to set `Role` leaves the stored `Role` unchanged | Service | **Yes** |
| Provisioning is idempotent (same `oid` twice → one row) | Service | **Yes** |
| An over-long claim is truncated, not thrown on | Service | **Yes** |
| Preference allowlists: out-of-range theme/language rejected; both default when omitted | Service | **Yes** |
| `DisplayName` non-blank after trim; whitespace-only `Description` normalises to null | Service | **Yes** |
| Avatar validation reuses the cover allowlist and size cap | Service | **Yes** |
| Replacing an avatar deletes the previous blob; the delete runs *after* the row moves | Service | **Yes** |
| `RemoveAvatarAsync` with no avatar makes **zero** storage calls, not merely a 200 | Service | **Yes** |
| The new `users` columns round-trip; preferences default rather than persist as null | Repository | **Yes** |
| No token → 401, and **no `users` insert occurs** (rejection precedes the write) | Api | **Yes** |
| `GET /user/me` returns the profile fields; a display-name change is visible on the next read | Api | **Yes** |
| The duplicate-key race's `SqlException` catch recognises what SQL Server throws | — | **No** — needs a database |
| The narrowing `ALTER COLUMN` against a populated table | — | **No** — needs an environment |
| An avatar is retrievable by a credential-free HTTP GET from the real container | Storage | **No** — needs credentials |

The last three are evidence debt, not test debt: no offline test can settle them. They are described
in the feature file under "Three things no offline test can prove" and belong to
[feature 11](../features/11-e2e-verification.md).

## Testability

**testable** for everything in the top twelve rows. `doc-assertion` for the reflection guard — it
asserts a property of a *type definition*, not a behaviour, and is the one test here whose value is
entirely in failing when a field is added later.

The bottom three rows are **verification-only** in this tier; they close against feature 11's
environment, not here.

## Repair plan

1. **Write the reflection test first.** It is the highest-value assertion in the item, red-first is
   impossible for it (the property is absent, so it passes immediately) — which means its value is
   only realised the day someone adds `Role` to the DTO. Say that in the test's comment so a reader
   does not "simplify" it away.
2. Work the service-tier rows next, in one pass: they share a fake `IUserRepository`,
   `IStorageRepository` and `IUserContextService`, so the setup cost is paid once.
3. Then the repository and API tiers.
4. Run `/add-test 02` if it still resolves — the command was built for exactly this shape of gap, and
   the feature file's `## Tests (TDD)` block is its input. Note the file is now in `archive/`, so the
   command's path resolution may need to be pointed at it explicitly.
5. When the offline rows are green, decide the fate of the bottom three: leave them recorded here as
   routing to 11, or close them with 11's environment if it exists by then.

## Out of scope / related

- **Feature 03 must not be treated as safe to build on until this closes.** The sprint file says so;
  this item is the mechanism. 03 changes `Role`'s column constraint, which is precisely the field
  the missing reflection test guards.
- **Item [04](04-usercontroller-route-convention.md)** changes the profile routes these Api-tier
  tests will assert. If 04 lands first, write the tests against the resolved paths.
- **Feature 11** owns the three unprovable assertions. This item should not be closed while claiming
  them; if 11 has not run, close this item on the twelve offline rows and let the three stay recorded
  in the feature file.

## Close checklist

- [ ] The reflection test exists and is commented as to why it can pass from day one
- [ ] Every "Now? Yes" row in the sub-table above is covered by a test that was seen to fail
- [ ] The storage tier's public-read assertion either exists or is explicitly deferred to feature 11
- [ ] The feature file's `## Testing status` and unmet `[ ]` criteria updated to reflect what landed
- [ ] STANDARD §12.12's claim re-verified: if it still says the tests are missing, it is correct; if
      they exist, this item closes
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
