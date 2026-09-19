# 27 — `Category!=Container` is not the offline run, and the name invites the mistake

Status: **open** · Kind: docs · Impact: cosmetic · Area: Docs
Source: found 2026-09-18 (during the container-backed test change) · Discharges via: — (orphaned) · Opened: 2026-09-18 · Last verified: 2026-09-18

## What the debt is

`Category=Container` is the only trait in the solution, which makes two filters look like complements
when they are not:

| Filter | Selects | Count (2026-09-18) |
|---|---|---|
| `--filter "Category!=Container"` | tests touching **neither** container | **20** (16 repository + 4 Api) |
| `dotnet test` with nothing configured | everything runnable | **31 passed, 28 skipped** |

The difference is the storage tier. Storage falls back to the Azurite emulator, which needs no
secret, so an unconfigured machine runs those 11 tests. `Category!=Container` therefore **excludes
tests that do run offline**, and the plan this change was built from described that filter as "the
pre-change offline run" — which it is not, and which is how the mislabelling was found.

## Why it matters

**A filter that reads as "the tests that work without containers" is a filter that skips eleven of
them.** Someone reaching for a fast local check will get a green run that omitted the storage seam,
and the run looks complete: 20 passing tests and no skips reported, because a filter *excludes*
rather than skips. A skip is visible in the summary; an excluded test is not.

**The counts in the table above drifted again on 2026-09-19, one feature later**, because feature 03
added tests to both container-backed tiers. Only the numbers moved; the filter semantics this item
is about did not, which is the item's own point about numbers being the least durable part of it.
They are not restated here — that is what let them go stale — and
[testing-and-tdd.md](../testing-and-tdd.md) is now their only home. Evidence for the repair below,
not a new defect.

**The cost is bounded, which is why it is cosmetic.** The documents are corrected as of 2026-09-18 —
[testing-and-tdd.md](../testing-and-tdd.md), [STANDARD.md](../../src/api/STANDARD.md) §10, the README
and the sprint commands all state what the filter selects and warn against reading it as the offline
run. So the divergence between the doc and reality is closed; what remains is that **the filter name
still invites the error**, and a reader who arrives at the command without the surrounding prose has
nothing to correct them.

## Evidence

Checked 2026-09-18, on this branch.

- `dotnet test --filter "Category!=Container"` → 16 repository + 4 Api = **20 passed, 0 skipped**.
- `env -u MSSQL_SA_PASSWORD -u TRAILBLAZE_SQL_CONNECTION -u TRAILBLAZE_STORAGE_CONNECTION dotnet test`
  → **31 passed, 28 skipped** out of 59.
- The 11-test difference is the storage tier, which runs against the emulator fallback.
- `Category` is the only trait applied anywhere in the solution, verified by enumerating traits.

## Testability

**doc-assertion.** The invariant is "the documents that describe these filters say what each one
selects, and do not present `Category!=Container` as the offline run". A test can read the files and
assert the statement is present, going red when one is reverted — the shape STANDARD §10 sanctions
for a durable property of a file, and the same one [item 19](19-doc-indexes-drifted.md) wants for
counts that drift.

The counts themselves drift with every added test, so the assertion should target the *claim* (which
tests the filter selects) rather than the number.

## Repair plan

1. **Decide whether the trait names the right thing.** Alternatives that do not invite the error:
   run the container tests by exclusion but document the offline run as *plain `dotnet test` with
   nothing configured* — which is what it actually is — and never present the filter as a substitute.
   This is largely done.
2. Optionally split the trait so the storage and database tiers are separately selectable
   (`Category=Container` plus a second trait per tier). That makes "storage only" expressible and
   removes the need for the misleading complement, at the cost of a second trait and a rule about
   applying both.
3. Add the `doc-assertion` test from the section above, and let [item 19](19-doc-indexes-drifted.md)
   own the numbers.
4. If (2) is taken, the counts in [testing-and-tdd.md](../testing-and-tdd.md) and STANDARD §10 need
   re-measuring in the same commit.

**Recommendation: (2) and (3), in that order.** A second trait is a small, honest change that makes
the useful selection expressible; without it the only way to say "skip the database, run storage" is
to name a trait that means something else.

## Out of scope / related

- **[Item 19](19-doc-indexes-drifted.md)** owns documentation counts that drift. This item owns the
  one *semantic* claim that was wrong rather than merely stale — which is why it is separate, and why
  fixing the number would not have fixed it.
- **A skip and an exclusion are not the same thing**, and this item is the worked example. The
  container tiers skip visibly; a filter removes silently.

## Close checklist

- [ ] No document presents `Category!=Container` as the offline run
- [ ] The tier that actually runs offline is describable without naming a filter that excludes it
- [ ] A test asserts the claim, not the count
- [ ] Counts in testing-and-tdd.md and STANDARD §10 re-measured if the traits changed
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
