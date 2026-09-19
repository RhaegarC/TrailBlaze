# 25 — `TrailBlaze.Service.Test` has no tests, and no way to host the ones it needs

Status: **open (narrowed by feature 03)** · Kind: test-gap · Impact: friction · Area: Tests
Source: found 2026-09-18 (during the container-backed test change) · Discharges via: features 06/08 (in part) · Opened: 2026-09-18 · Last verified: 2026-09-19

## Update — 2026-09-19 (feature 03)

Feature 03 needed both halves of this item and so closed fact 1 and answered the decision, taking
**(a)** rather than the **(c)** recommended below. Recorded here rather than diverged from in
silence; the reasoning is in [03-admin-seeding.md](../features/03-admin-seeding.md#decisions).

**Fact 1 is closed.** `TrailBlaze.Service.Test` now holds six tests in two files: `CallerRoleTests`
(4, container) covers role resolution against a real database, and `RoleComesFromTheRowTests`
(2, offline) asserts by reflection that the role has no other source. Measured after the change:
2 passed / 4 skipped / 9 ms offline, 6 passed / 937 ms with the containers up. *(An earlier revision
of this block counted nine, three of them in an `AdminSeedingTests` file. Feature 03's startup
seeder was removed the same day, and that file with it — see
[03-admin-seeding.md](../features/03-admin-seeding.md#decisions).)*

**Decision: (a) — the service tier took the repository reference.** The recommendation below
preferred (c) and rested on a cost that the measurement above does not show: 937 ms for six
container-backed tests is not a tier "slow to the point of changing how the tier is used", and the
offline run is unchanged for anyone without containers, because the four store-backed tests skip
rather than fail. Against that, (c)'s own drawback is the one that decided it — the assertions that
matter here are security ones ("the caller's role comes from the row, never the token", "no caller
without a row is granted one"), and (c) would have filed them as the repository's behaviour, which
is where nobody looks for an authorization rule. The tier rule in
[testing-and-tdd.md](../testing-and-tdd.md) — a test goes in the project matching the layer it
exercises — then applies without an exception.

**What was bought and what was paid.** The reference is a `ProjectReference` to
`TrailBlaze.Repository.Test`, not to `TrailBlaze.Repository`, so the service tier still cannot reach
the `DbContext` or the storage implementation in production code; it reaches the *fixtures*, which is
the smallest thing that works. The cost is that a test project now references another test project,
which is the arrangement this item's related [item 16](16-unreferenced-scaffolding.md) would object
to if it had no consumer; it has six. A shared `TestSupport` project is the tidier shape and is
left as a follow-up rather than attempted inside a feature.

**Still open, and what it still owns.** The four spec bullets in the table above are unaffected —
features 06 and 08 are unbuilt and their tier is now answered by precedent: a store-backed service
claim runs beside the service. The bullets should be re-pointed at this project when those features
start, and `testing-and-tdd.md`'s tier table now accounts for it.

## What the debt is

Two related facts, and the second is the one that costs.

*Both facts below are as filed on 2026-09-18. Fact 1 no longer holds — see the update above.*

**1. The project contains no tests.** `src/api/TrailBlaze.Service.Test/` holds a `.csproj` and build
output, nothing else — no `.cs` file at all. `dotnet test` still discovers and reports the project as
part of a green suite, so "all tests passing" is true and says nothing about the service layer.

**2. It cannot host the assertions the service specs call for.** `TrailBlaze.Service` references only
`TrailBlaze.Interface` and `TrailBlaze.Model`, and `TrailBlaze.Service.Test` references
`TrailBlaze.Service`, so the tier has no `DbContext`, no `IStorageRepository` implementation, and no
engine. Every service-layer claim that is decided from a request — allowlists, size bounds, `Kind`
derivation, URL shape — can live here. Every claim that is *about a store* cannot:

| Claim the specs want | Tier that could host it | Currently |
|---|---|---|
| feature 06 — the per-activity count cap at 19/20/21 | needs a query, so an engine | no home |
| feature 06 — a refused upload leaves no orphan blob | needs a listable store | no home |
| feature 08 — the destination container per `Type` | needs an observable store | no home |
| feature 08 — the move leaves the source empty | needs an observable store | no home |

Those four bullets were written against the deleted `IStorageRepository` fake, which is why they had
a home before and do not now.

## Why it matters

**The service layer is where the security boundary will live, and it is the one tier with no tests.**
Feature 09's ownership and permission evaluation is service-layer logic and is a documented security
hot spot
([testing-and-tdd.md](../testing-and-tdd.md#tdd-discipline)); its tests are meant to land here. So
this item is not "a project is under-used" — it is the tier that will hold the app's most
security-relevant assertions, currently unable to run any of them.

**Nothing is wrong today, which is why it is debt.** No service-layer behaviour has shipped untested
in a way a user can see: feature 02's service work is covered by [item 12](12-feature-02-tests-deferred.md)'s
deferred set, and features 06 and 08 are unbuilt. What is wrong is the *shape*: four spec bullets
name a tier they cannot run in, and an implementer following them will discover that only after
writing the tests.

**The obvious fix is a decision, not a reference.** Adding `TrailBlaze.Repository` to the service
test project would give it a context and a storage implementation, and would also be the
reference-with-no-consumer [item 16](16-unreferenced-scaffolding.md) exists to complain about if it
lands before the first test that needs it.

## Evidence

Checked 2026-09-18.

- `ls src/api/TrailBlaze.Service.Test/` → `.csproj`, `bin`, `obj`. No source files.
- `dotnet test` reports the project in the run and adds 0 to the total; the solution's 59 tests were
  55 repository + 4 Api. (It is 76 now: 9 here, and the counts below were re-measured.)
- `TrailBlaze.Service.Test.csproj` lists no `ProjectReference` to `TrailBlaze.Repository`.
- The four spec bullets above were re-pointed on 2026-09-18 and each carries a note saying its tier
  is unsettled.

## Testability

**verification-only** for fact 1 — "this project has no tests" is not a behaviour, and a test
asserting it would go red for the wrong reason the moment the fix lands. It closes by writing tests.

The repair itself is testable in the ordinary way: the first service-layer test that runs in this
project is the evidence.

## Repair plan

1. **Decide where a service-layer claim that needs a store runs.** Two shapes, and they are not
   equivalent:
   - *(a)* **Service tier gets the repository reference.** Service-layer assertions run where the
     logic under test lives, which is what the specs assume. Costs the service tier a container
     dependency, so it stops being fast and offline.
   - *(b)* **Those claims stay in `TrailBlaze.Repository.Test`.** The tier that already owns the
     containers and the fixtures, but the logic under test is a layer above the tier that exercises
     it, which is what the suffix rule exists to prevent
     ([codereview.md](../../.claude/rules/codereview.md)).
   - *(c)* **Split by claim kind:** decisions from a request stay in the service tier offline;
     store outcomes live in the repository tier, described as *the repository's* behaviour rather
     than the service's. Honest, and it means feature 08's routing table is asserted somewhere other
     than where it is implemented.
2. Answer it before features 06 and 08 start, since four bullets depend on it and each was written
   assuming a fake that no longer exists.
3. Add the reference (or don't) in the feature PR that needs it, not ahead of it.
4. Record the answer in [testing-and-tdd.md](../testing-and-tdd.md)'s tier table, which currently
   lists three projects and gives no account of this one.

**Recommendation: (c).** A service test project with a container dependency is slow to the point of
changing how the tier is used, and the alternative — testing a service's routing by asserting the
repository's state — is a real, describable claim. The cost is that the spec bullets must name
*whose* behaviour they assert, which is worth doing anyway.

## Out of scope / related

- **[Item 12](12-feature-02-tests-deferred.md)** owns feature 02's missing tests, some of which land
  here. That is the first real content this item expects.
- **[Item 16](16-unreferenced-scaffolding.md)** owns the scaffolding smell; this item is where its
  warning applies most directly.
- **The project should not be deleted.** Removing it would leave service-layer tests with no
  declared home and hide the gap rather than close it.

## Close checklist

- [ ] The project contains at least one test, or the reason it does not is written down
- [ ] A service-layer claim that needs a store names the tier that runs it
- [ ] The four spec bullets point at a tier that can host them
- [ ] The tier table in [testing-and-tdd.md](../testing-and-tdd.md) accounts for this project
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
