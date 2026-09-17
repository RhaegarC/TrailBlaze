# 14 — The warning-free build rule has nothing enforcing it

Status: **open** · Kind: process · Impact: friction · Area: Build
Source: found 2026-09-17 (not a §12 item) · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

STANDARD §1 states the rule ([STANDARD.md:110-111](../../src/api/STANDARD.md#L110-L111)):

> a build is expected to be warning-free; fix the warning rather than suppressing it

Nothing enforces it. There is no `<TreatWarningsAsErrors>` in any `.csproj`, no `Directory.Build.props`,
and no `.editorconfig` anywhere in the repository (all three verified absent 2026-09-17).

The build is warning-free today — that is the honest starting position, and it is why this is
`friction` rather than `silent-wrong`. The rule holds because people honour it, not because anything
checks.

## Why it matters

**A rule with no enforcement decays at the rate of the least careful contributor**, and it decays
invisibly: warnings accumulate one at a time, each looking harmless in a PR that has no reason to
mention them. The standard's own §10 argument applies to it — a rule whose reasoning is not carried
by a mechanism gets simplified away.

This register is the evidence. §12.1 sat false in a document everyone trusted because nothing checked
it; the same shape of gap here is a standard stating a property of the build that no build asserts.
The difference is that this one is fixable in a single file.

**It also interacts with the compiler warnings that matter most.** The codebase just removed a
`CS9107` warning by hand during feature 02 — a captured primary-constructor parameter that would have
shipped a silently duplicated `DbContext`. That class of warning is exactly what a
`TreatWarningsAsErrors` catches before review does, and the fix that resolved it was a design change
someone had to notice. Under an enforcing build it would have been impossible to merge.

## Evidence

Checked against the repo 2026-09-17.

- No `Directory.Build.props`, no `.editorconfig`, no `TreatWarningsAsErrors` in any `.csproj` (glob
  and grep, both empty).
- No `NoWarn`, `#pragma warning disable`, or `[SuppressMessage]` either — so the codebase is not
  currently suppressing anything, which makes this the cheapest possible moment to turn enforcement
  on. A rule added now costs zero suppressions to adopt; a rule added later inherits whatever has
  accumulated.
- `dotnet build` reports **0 warnings, 0 errors** as of 2026-09-17.

## Testability

**verification-only** for the build behaviour, plus **doc-assertion** for the rule's existence.

The property under repair is "the build fails when a warning appears", which cannot be asserted from
inside the test suite — the suite runs after the build succeeds. The honest verification is
mechanical and takes a minute: introduce a warning, run `dotnet build`, observe the failure, remove
it. Record that in the PR body.

A `doc-assertion` test could pin that the standard still states the rule, but that is item
[19](19-doc-indexes-drifted.md)'s mechanism, not this one's.

## Repair plan

1. **Decide where enforcement lives.** `Directory.Build.props` at `src/api/` applies to every project
   including the test projects — which is usually what you want, but it means a warning in a test
   also fails the build. Confirm that is intended before adding it.
2. Set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Consider `WarningsNotAsErrors` for any
   warning the team judges advisory — but note that adding an exception list on day one, when there
   is nothing to suppress, is how the rule starts eroding.
3. **Verify it actually fails**: introduce a warning (an unused variable is enough), run
   `dotnet build`, confirm the failure, remove it, confirm green.
4. **Consider whether CI should carry it too** — it is the natural home, but item
   [11](11-no-ci-pipeline.md) does not exist yet and this fix must not wait for it. Local enforcement
   is the larger half anyway: it fails before the commit rather than after the push.
5. Re-check the standard's wording. "Fix the warning rather than suppressing it" is a rule about
   `NoWarn`, which enforcement makes unnecessary — amend §1 if the mechanism supersedes the prose.

## Out of scope / related

- **Item [11](11-no-ci-pipeline.md)** is where the enforcement would ideally also run. Neither blocks
  the other; doing 14 first means 11 has one less thing to add when it lands.
- **`.NET 10 preview warnings`** are the plausible reason the rule was left unenforced. If the
  first `dotnet build` after adding `TreatWarningsAsErrors` fails on SDK-supplied warnings rather
  than on the code, say so in the PR and scope the exception narrowly — that is a real finding, not a
  reason to abandon the item.
- **Analyzers are a separate decision.** `TreatWarningsAsErrors` raises the severity of warnings that
  already exist; enabling additional analyzer rules is a different change with its own cost. Not this
  item.

## Close checklist

- [ ] A warning was introduced and the build was observed to fail
- [ ] The build is green again with enforcement on, with no suppressions added
- [ ] Whether enforcement covers the test projects is stated
- [ ] STANDARD §1's wording re-checked against the mechanism
- [ ] `Verification:` line naming the command run and the observed result
- [ ] "No test — and why" section in the PR body
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
