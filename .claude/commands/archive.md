---
description: Archive a completed feature or tech-debt item after its PR is merged to develop
argument-hint: "[number]"
---

You are executing the TrailBlaze `/archive` command (Phase 8 of the `tdd-implement` agent — [.claude/agents/tdd-implement.md](../agents/tdd-implement.md)).
`$ARGUMENTS` is the feature number (e.g. `06`) — resolve it to
`docs/features/<number>-<name>.md`.

**Only archive after the feature PR is merged to `develop`:**
1. **Verify the merge** — confirm the feature's changes are in `origin/develop` (check
   `git branch -a --contains <feature-branch>` on `develop`/`origin/develop`, or that the
   PR is closed/merged). If **not merged, stop** and explain why.
2. Create `docs/features/archive/` if it doesn't exist.
3. Move the file: `git mv docs/features/<file> docs/features/archive/<file>`.
4. Confirm the Phase 1 selection scan now skips the archived feature.
5. Report the archived feature and suggest updating sprint tracking
   (`docs/features/00-mission-1-sprint.md`).

**Before archiving feature 09, check the deployability gap closes.** Features 04–08 shipped
permissive, so 09 is the one that makes `develop` safe. If 09 is being archived, confirm the
permission matrix in the PRD is fully covered by merged tests — that is the evidence, not the
feature file's checkboxes.

## Archiving a tech-debt item

The `debt-fix` agent does this as its step 8; this is the same move, done by hand when an item
resolves without a fix of its own. `$ARGUMENTS` is the debt number — resolve it to
`docs/tech-debt/<number>-<name>.md`.

1. **Set the item's `Status`** to a `**Archived** — …` line saying **how** it resolved: by a
   feature (name it and its PR), by another debt item, or by finding that the divergence no longer
   exists. That last case is a real outcome, not a failed lookup — say so plainly.
2. `git mv docs/tech-debt/<file> docs/tech-debt/archive/<file>`.
3. **Update the register — this is the step that is easy to skip and costs the most.** In the same
   commit: delete the item's row from the open table in
   [00-debt-log.md](../../docs/tech-debt/00-debt-log.md) and add a row to its **Archive** table.
   The log is an index, and a row pointing at a file that has moved is precisely the drift this
   register was created to stop — see item 19.
4. If any other item named this one in **Discharges via**, resolve those rows too: they were facets
   and close with it, or they need an owner and become `— (orphaned)`.
