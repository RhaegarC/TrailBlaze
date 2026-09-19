---
description: Show sprint progress, DoD status, branches, and open pull requests
---

Produce a concise sprint status report for TrailBlaze:

1. **Feature progress** — list every feature in `docs/features/` (01–11) with status
   (not started / in progress / archived), based on file locations and git state.
2. **Sprint DoD** — check the Definition of Done items in
   `docs/features/00-mission-1-sprint.md` against what is actually complete (layered solution
   builds green? Entra auth + auto-provisioning + an admin row? activity CRUD with validation?
   anonymous paged list? private media upload within caps? SAS delivery authenticated-only?
   public covers? ownership + admin rules enforced? Figma app integrated? E2E verified?). Show
   checked vs. unchecked.
3. **Debt queue** — read the open table in [docs/tech-debt/00-debt-log.md](../../docs/tech-debt/00-debt-log.md)
   and report its size, how many items are `— (orphaned)`, and any item whose `Discharges via` names
   a feature that has since been **archived without discharging it**. That last check is the one
   that matters: a feature file moving to `archive/` is not evidence it closed the items it owned,
   and nothing else watches the seam — a feature can archive green while the item it was going to
   discharge silently becomes nobody's.
4. **Git state** — current branch, recent commits (`git log --oneline -5`), open `feature/*`,
   `fix/*` and `debt/*` branches, and any open pull requests (`gh pr list`).
5. **Next actions** — recommend the next feature to pick up and any blocked DoD items.

Present as a short table plus a two-line "next action" summary.

**Two honest caveats to surface whenever they apply:**

- **The deployability gap.** Features 04–08 build the mechanics while every authenticated user
  can still write anything. Until feature 09 (permission enforcement) is merged, `develop` must
  not be treated as a usable environment. If 09 is not done, say so in the summary rather than
  reporting a clean-looking sprint.
- **The container tiers, and the difference between a skip and a pass.** The database and storage
  tiers need `docker-compose.test.yml` running; without it they **skip**, and a skip is not a pass.
  The default `dotnet test` on a bare machine therefore reports 31 passed and 28 skipped, and
  quoting only the pass count would overstate what ran. There is no storage fake to fall back on —
  when the container is down, the blob implementation is simply untested. Report the skipped count
  alongside the passed count, and never describe a run with skips as "all tests passing".
