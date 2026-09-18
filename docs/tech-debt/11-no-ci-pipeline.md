# 11 — There is no CI or deployment pipeline, and it is the only path to production

Status: **open** · Kind: capability · Impact: blocks · Area: Workflow
Source: STANDARD §11 + §12.11 · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

There is no `.github/` directory. Nothing builds on push, nothing runs the suite on a pull request,
and nothing deploys.

Both real targets are nonetheless specified as GitHub-workflow destinations: the API to Azure
Container Apps, the web app to Azure Static Web Apps. The `Dockerfile` for the ACA image exists and
builds. So the pipeline is not merely unwritten — it is **the only mechanism either component has for
reaching production**, and it does not exist.

A consequence that is easy to miss: **applying a migration is a manual step.** The deployment
pipeline is what runs `dotnet ef database update` before the new revision takes traffic. With no
pipeline, that is a human running a command against a live database, outside any recorded process.

## Why it matters

**Nothing verifies a pull request.** This register's own workflow depends on review comments, and the
project's rule is that a PR merges once every comment is addressed — but the only automated check is
whatever the reviewer remembers to run. `dotnet test` is run by hand, when someone thinks to. A PR
that breaks the build can merge clean, and the break is found by whoever pulls `develop` next.

**The container tiers have no place to run, and 2026-09-18 changed what that costs.** The storage
tier used to be the one tier that needed credentials, so "unproven in CI" was a funding problem as
much as a pipeline one. It no longer is: it runs the real `AzureBlobStorageRepository` against the
Azurite emulator, which carries no secret, so **CI could run it tomorrow with no Azure account at
all** — and the database tier likewise, against SQL Edge. What was a credential gap is now purely a
missing pipeline, which makes this item the whole of the obstacle rather than half of it. Without
one there is not even a place to run them.

**STANDARD §11 describes a workflow that does not exist**, which makes the standard a document about
an imagined system in exactly the way §12.1 was. §11's "State as of 2026-09-15" note is honest about
this; the section title is not.

**The lesson from the foundation feature is worth carrying:** an `IHostedService` that migrated at
startup was built and then removed, because ACA runs several replicas and concurrent startup
migrations race over the same DDL ([PRD.md:406-408](../PRD.md#L406-L408) — the passage used to sit in
STANDARD §12, which is now this register). The
pipeline is not a convenience that replaces that; it is the design that made removing it safe. Until
it exists, the removal left a gap rather than a replacement.

## Evidence

Checked against the repo 2026-09-17.

- No `.github/` directory; `git ls-files` returns no workflow file.
- Deploy targets named in [PRD.md](../PRD.md) ("Web hosting … not yet built") and in
  [00-mission-1-sprint.md](../features/00-mission-1-sprint.md)'s "Where the code actually stands":
  *"Still absent: no CI pipeline, which 01 does not claim and which is now the only path either
  component has to production."*
- [01-foundation.md](../features/archive/01-foundation.md) lists "no CI pipeline definition" among
  its non-goals, so no feature has ever claimed this work.
- STANDARD §11 carries a state note admitting the workflow is not wired
  ([STANDARD.md:658](../../src/api/STANDARD.md#L658)).

## Testability

**verification-only.** The deliverable is a workflow file whose correctness is observed by pushing
and watching it run — not assertable from the test suite, which cannot observe GitHub's runners.

The honest verification is the pipeline's first real execution: a PR that shows the build and test
jobs running and reporting. The close checklist requires that link. If the pipeline is added but
never observed running against a PR that would have failed, it is unverified in the same way §11 is.

## Repair plan

1. **Scope decision first.** STANDARD §11 describes **two** jobs and says so explicitly: the template
   job "belongs to the upstream template repository this solution was generated from: there is no
   `SampleTemplate/` and no template package here to pack, so only the first job is in scope"
   ([STANDARD.md:536-540](../../src/api/STANDARD.md#L536-L540)). Build the build-and-test job; leave
   §11's second job described-but-absent or remove it from the standard.
2. **Build and test** on push and PR: `dotnet build` and `dotnet test` from `src/api/`, failing on
   the warnings the standard already requires to be zero. This is the item that makes
   [14](14-warning-free-build-unenforced.md) enforceable, so consider them together.
3. **Decide the storage tier's fate in CI** (item [12](12-feature-02-tests-deferred.md)): either
   supply credentials as secrets so the tagged tier runs, or state in the pipeline that it does not
   and why. The failure mode to avoid is a tier that silently skips in CI and is believed to pass.
4. **The deployment workflow** for ACA and Static Web Apps, including `dotnet ef database update`
   before the new revision takes traffic. This is the half that replaces the manual migration step.
5. Update STANDARD §11 to describe what exists, and remove the state note if it no longer applies.

## Out of scope / related

- **The template job** is explicitly out of scope; there is no template in this repository.
- **Item [14](14-warning-free-build-unenforced.md)** is a rule with nothing enforcing it. A CI
  pipeline is the natural place for that enforcement, but 14 is fixable without CI (a
  `Directory.Build.props` fails the build locally too), so neither blocks the other.
- **Item [12](12-feature-02-tests-deferred.md)** needs a CI home to close its "unprovable offline"
  half. This item is a prerequisite for that, which is why 12 records `Discharges via: 11 (in part)`.
- **Azure credentials** are genuinely required for the storage tier and are not a code problem —
  an operator decision this item surfaces rather than resolves.

## Close checklist

- [ ] A PR shows the build and test jobs executing and reporting
- [ ] The build fails on a warning, or the standard's warning rule is amended to say it does not
- [ ] The storage tier either runs with credentials or is explicitly skipped in writing, not silently
- [ ] The deployment workflow runs `dotnet ef database update` before the revision takes traffic
- [ ] STANDARD §11 rewritten to describe what exists; the state note removed or updated
- [ ] `Verification:` line naming the PR where the pipeline was observed, and its result
- [ ] "No test — and why" section in the PR body
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
