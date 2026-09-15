---
description: Show sprint progress, DoD status, branches, and open pull requests
---

Produce a concise sprint status report for TrailBlaze:

1. **Feature progress** — list every feature in `docs/features/` (01–11) with status
   (not started / in progress / archived), based on file locations and git state.
2. **Sprint DoD** — check the Definition of Done items in
   `docs/features/00-mission-1-sprint.md` against what is actually complete (layered solution
   builds green? Entra auth + auto-provisioning + admin seeded? activity CRUD with validation?
   anonymous paged list? private media upload within caps? SAS delivery authenticated-only?
   public covers? ownership + admin rules enforced? Figma app integrated? E2E verified?). Show
   checked vs. unchecked.
3. **Git state** — current branch, recent commits (`git log --oneline -5`), open `feature/*`
   branches, and any open pull requests (`gh pr list`).
4. **Next actions** — recommend the next feature to pick up and any blocked DoD items.

Present as a short table plus a two-line "next action" summary.

**Two honest caveats to surface whenever they apply:**

- **The deployability gap.** Features 04–08 build the mechanics while every authenticated user
  can still write anything. Until feature 09 (permission enforcement) is merged, `develop` must
  not be treated as a usable environment. If 09 is not done, say so in the summary rather than
  reporting a clean-looking sprint.
- **The storage integration tier.** It needs live Azure credentials to run. Without them, CI
  proves the in-memory fake, not the real blob implementation — so "all tests passing" is a
  weaker claim than it looks. Report which tier actually ran if you can tell.
