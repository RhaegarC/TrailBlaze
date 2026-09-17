---
description: Capture a feature spec, bug report, or tech-debt item into docs/ (requirements, bug & debt capture workflow)
argument-hint: "[feature|bug|debt] [NN]"
---

You are executing the TrailBlaze `/capture` command — the Requirements, Bug & Debt Capture workflow.
It creates the spec files that the feature, bug and debt pipelines then consume: a prioritized
idea becomes `docs/features/NN-name.md`; a reported bug becomes `docs/bugs/NN-name.md`; a divergence
between the code and the standard becomes `docs/tech-debt/NN-name.md`. Run it **before** feature
selection (Phase 1 of the `tdd-implement` agent), bug triage (the `bug-fix` agent), or a debt fix
(the `debt-fix` agent).

`$ARGUMENTS` is a hint like `feature 04`, `bug 02`, `debt` or `debt 13`. Clarify with the user what
is being captured.

1. **Clarify** the item before writing anything — the artifact depends on it:
   - **Feature** → use the `/grill-me` skill to stress-test the idea into scope, acceptance criteria, and success metrics.
   - **Bug** → run a triage checklist, not grilling (repro steps, expected vs actual, severity, environment/version, component), and create/note the Azure DevOps work item ID.
   - **Debt** → run an **evidence pass**, not grilling and not triage: re-check the claim against the code
     as it is now, and record the `file:line` you checked it at plus the date. See below.
2. **Branch** off `develop`: `docs/feature-[nn]-[name]`, `docs/bug-[nn]-[name]`, or
   `docs/debt-[nn]-[name]`.
3. **Write the file** using the established template:
   - **Feature** → `docs/features/NN-name.md` following the structure of the existing feature files
     (see [docs/features/archive/01-foundation.md](../../docs/features/archive/01-foundation.md) for the shape: status,
     summary, story, dependencies, acceptance criteria, tests, non-goals). The number claims the priority
     slot — only spec features in implementation order; vague ideas stay in `backlog.md`.
   - **Bug** → follow the bug file template (triage, reproduction, fix plan, close checklist); also add a
     row to `docs/bugs/00-bug-log.md`.
   - **Debt** → follow the register's item template — `Status` / `Source` / `Discharges via` /
     `Opened` / `Last verified`, then **What the debt is**, **Why it matters**, **Evidence**,
     **Testability**, **Repair plan**, **Out of scope / related**, **Close checklist**; also add a row
     to `docs/tech-debt/00-debt-log.md` and read its kind/impact/testability guides first.
4. **Commit & push**: `docs:` commit type; push the branch.
5. **Create the pull request** (`gh pr create --base develop`) to `develop` quoting the acceptance criteria
   (feature), the repro + severity (bug), or the claim and where it was checked (debt). Do NOT merge
   without approval.
6. **Report** the branch + PR link. After the PR merges, close out (mirrors Phase 8 of the `tdd-implement`
   agent): `git checkout develop && git pull --prune origin develop`, then `git branch -D` the spec branch.

**Every feature file must respect the PRD.** `docs/PRD.md` holds the decisions log, the canonical data
model, and the permission matrix. A feature that contradicts it is either wrong or is proposing a
decision change — raise that explicitly rather than writing a spec that quietly disagrees.

## Capturing debt

`/capture debt` has three modes, and the difference between them matters more than it does for
features and bugs — because the register's first act was discovering that a claim it inherited had
been false for months. **A debt item is a claim, and a claim is only as good as the last time
somebody checked it.**

- **`debt`** — **file a new item.** An evidence pass: find the `file:line` where the divergence
  lives and read it, rather than filing from memory or from another document's summary of it. Then:
  - **Search for an owner before filing.** Does a `docs/features/NN-*.md` file, an open `feature/*`
    branch, or an existing debt item already resolve this? If so, set `Discharges via` to it rather
    than creating a second thing to track — an item already in flight is not an orphan, and filing
    it as one is how a register starts racing the work that is fixing it.
  - **Check it belongs here.** If the divergence is failing now, or would let private media or an
    Entra object id reach the wrong caller, it is a **bug**, not debt: `/capture bug`, `bug-fix`
    agent, Critical by default. Debt never takes `hotfix/`.
  - Take the next free number — check `archive/` too, since archived numbers are spent — and read
    the log's kind/impact/testability guides before writing the row.
- **`debt NN`** — **verify an existing item.** Re-check its claims against the code as it is now,
  then either amend its `Evidence` with what you found or, if the divergence is gone, **retire** it.
  This is the mode §12.1 needed and never got.
- **`retire NN`** — **close an item whose divergence no longer exists.** `git mv` it to
  `docs/tech-debt/archive/` with a `Status: **Archived**` line saying how it resolved, then in the
  same commit delete its row from the register's open table and add a row to the Archive table. A
  retirement with no code change is a legitimate outcome — say so in the PR body rather than
  manufacturing a fix to justify the trip.
