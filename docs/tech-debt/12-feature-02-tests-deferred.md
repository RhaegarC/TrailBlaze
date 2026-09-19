# 12 — Feature 02's behaviour shipped without the tests §10 requires

Status: **open (narrowed by feature 03)** · Kind: test-gap · Impact: blocks · Area: Tests
Source: STANDARD §12.12 · Discharges via: 11 (in part) · Opened: 2026-09-17 · Last verified: 2026-09-19

## Update — 2026-09-19 (feature 03)

**The highest-value assertion in this item now exists, and only for `Role`.** Feature 03 wrote
`RoleComesFromTheRowTests.The_profile_edit_request_offers_no_role`
([source](../../src/api/TrailBlaze.Service.Test/RoleComesFromTheRowTests.cs)) — a reflection
assertion that `UpdateProfileRequest` carries no `Role` property, commented with the reason it can
pass from day one, which is exactly what the repair plan's step 1 asked for. The close checklist's
first line is satisfied **for that one field**.

**`Email` and `Id` are still unguarded**, though the sub-table's row names all three. The test
asserts one property name, not the set; widening it is a two-line change and the cheapest thing left
in this item. Nothing about it is blocked.

**Why feature 03 did it rather than this item.** 03's own criterion is that no payload or claim can
make a caller an admin, and the row is what decides the role — so the guard is on 03's critical path,
not borrowed. It is filed here because this item is where the missing test was recorded and where the
prediction that 03 would need it was written.

**This item does not close on that.** Everything else in the sub-table is unwritten, including all
eleven "Now? Yes" rows that this item's own text says should not wait for anything.

## What the debt is

Feature 02 (Entra auth and the self-service profile slice) is implemented, merged to `develop` in
PR #4, and archived. Its tests were deliberately not written. By STANDARD §10 — *a behaviour change
without a test is not finished* — the feature is not finished, and its archived status records the
end of the document's lifecycle rather than a completed feature.

The two criteria that matter most were left explicitly unticked in the feature file, and both are
silent when wrong:

- **The privilege-escalation guard has no test.** `UpdateProfileRequest` has no `Role`, `Email` or
  `Id` property, and that absence is the *entire* control. Nothing would fail if a later change
  added one. (`Role` was covered on 2026-09-19 by feature 03 — see the update above. `Email` and
  `Id` were not.)
- **The avatar's public-read behaviour has no test**, so Decision #28's design intent is implemented
  but never fetched from the real container.

Recorded at [02-entra-auth.md#testing-status](../features/archive/02-entra-auth.md#testing-status).

## Why it matters

**An untested guard that everyone believes is tested is worse than one known to be untested**, which
is the feature file's own reasoning and is right. `Role` is not writable through the profile route
because a *property is absent from a DTO* — a shape no compiler, no schema, and no existing test
observes. The failure mode is a user promoting themselves to admin, silently and permanently — and
since feature 03 made the `users` row the only source of privilege, such a promotion would survive
every later check the app makes. That the `Role` half of the absence is now asserted is recorded in
the update above.

**It is a live baseline, not a historical gap.** `develop` carries this code. Features 03 and
following build on the `users` table and the profile slice, so their tests will assume behaviour that
nothing has proven.

**The feature is the demonstration of the problem this register exists for.** Its own file says an
untested guard everyone believes is tested is worse than one known to be untested *because the second
one gets written*. That argument is why this item is a blocking test-gap rather than a note.

## Evidence

Checked against the feature file and the suite 2026-09-17.

- The suite is 59 tests as of 2026-09-18 (**31 passed, 28 skipped** with nothing configured; 59 passed
  with the containers up). None of them covers feature 02's profile routes, the upload validator, or
  the provisioning conflict branch. Re-measured by running `dotnet test` 2026-09-18; the count was 42
  when this item was opened, which is what the older "42 tests, 41 passing, 1 skipped" recorded.
- [02-entra-auth.md](../features/archive/02-entra-auth.md) carries a `## Testing status` section, two
  unmet `[ ]` criteria, and a `## Tests (TDD)` section prefaced as unwritten — it is the plan for
  this item, not a description of coverage.
- The feature's own `Tests (TDD)` block names seven tiers' worth of tests. The sub-table below
  separates what is fixable now from what is not.

### What can be written now versus what cannot

The three items that needed an environment must not swallow the rest — most of this is ordinary
offline-testable work, and it should be done without waiting for anything. (As of 2026-09-18 two of
those three no longer wait for feature 11 either; see the re-stamp below the table.)

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
| The duplicate-key race's `SqlException` catch recognises what SQL Server throws | Container | **Partly** — see below |
| The narrowing `ALTER COLUMN` against a populated table | Container | **Yes** — `MigrationNarrowingTests` |
| An avatar is retrievable by a credential-free HTTP GET from the real container | Container | **Yes** — `StorageContainerRoutingTests` |

**Re-stamped 2026-09-18: the bottom three moved out of "unprovable offline" and into the container
tier.** They were evidence debt while nothing in the solution could reach an engine or an account,
and the sentence that used to sit here — *"no offline test can settle them"* — stays true and stopped
mattering, because the tier that settles them is not offline. What each one needs is now checked,
not assumed:

- **The narrowing `ALTER COLUMN`** is `MigrationNarrowingTests`, both halves: the failure (a
  300-character value against the narrowed column, asserting `SqlException` 8152/2628) and a passing
  variant, so the failing one cannot be red for an unrelated reason.
- **The credential-free avatar GET** is
  `StorageContainerRoutingTests.A_public_container_serves_its_object_to_an_unsigned_url`, a theory
  over `avatars` and `covers` that also asserts the URL carries no `sig=` — which is the part feature
  07 forbids, not merely the part it permits. Its negative control is the same URL shape against
  `media`.
- **The duplicate-key row is settled in part, and the part that is missing is feature 02's own.** The
  engine's half is measured: `DuplicateKeyTests` asserts the refusal is a duplicate-key `SqlException`
  (2627 for this schema, 2601 accepted) and that the loser leaves the winner's row readable. What
  remains unwritten is the *catch* — `UserService.GetOrCreateAsync` recognising that number and
  retrying, which is service-layer code and still has no test. So this row is not closed by the
  container tier; it is split, and the service half stays here.

The race itself was never in this table and is still not testable here: reproducing it needs two
simultaneous requests, which is load evidence and belongs to
[feature 11](../features/11-e2e-verification.md).

## Testability

**testable** for everything in the top twelve rows. `doc-assertion` for the reflection guard — it
asserts a property of a *type definition*, not a behaviour, and is the one test here whose value is
entirely in failing when a field is added later.

The bottom three rows are **testable** as of 2026-09-18 — two are already written and green against
the container tier, and the third is split, its engine half measured and its service half still
here. None of them is `verification-only` any more; the race alone remains so, and it was never a row
in this table.

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
5. ~~Decide the fate of the bottom three.~~ **Decided 2026-09-18:** they run in the container tier,
   which needs no credentials and no environment beyond the compose file, so none of them waits for
   feature 11. Two are written. The third closes only when the *service* half lands — the catch in
   `GetOrCreateAsync` that recognises the number `DuplicateKeyTests` measures.

## Out of scope / related

- **Feature 03 must not be treated as safe to build on until this closes.** The sprint file says so;
  this item is the mechanism. 03 changes `Role`'s column constraint, which is precisely the field
  the missing reflection test guards.
- **Item [04](04-usercontroller-route-convention.md)** changes the profile routes these Api-tier
  tests will assert. If 04 lands first, write the tests against the resolved paths.
- **Feature 11** owned the three unprovable assertions until 2026-09-18, when the container tier
  made two of them ordinary tests. **Feature 11 keeps only the race** — two simultaneous requests —
  and the deployed shape. This item no longer waits on 11 for anything it can prove itself.

## Close checklist

- [x] The reflection test exists and is commented as to why it can pass from day one — for `Role`,
      2026-09-19. Widening it to `Email` and `Id` is still open
- [ ] Every "Now? Yes" row in the sub-table above is covered by a test that was seen to fail
- [x] The storage tier's public-read assertion exists (`StorageContainerRoutingTests`, 2026-09-18)
- [ ] The service-layer duplicate-key catch is tested against the number `DuplicateKeyTests` measures
- [ ] The feature file's `## Testing status` and unmet `[ ]` criteria updated to reflect what landed
- [ ] STANDARD §12.12's claim re-verified: if it still says the tests are missing, it is correct; if
      they exist, this item closes
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
