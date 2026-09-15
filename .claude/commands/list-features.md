---
description: List all features with their current status
---

List every feature in `docs/features/` (including `docs/features/archive/` if it exists),
excluding the non-features `00-mission-1-sprint.md` and `backlog.md`.

For each feature show:
- **Number + name** (from the filename)
- **Priority** (lower number = higher)
- **Status**: not started / in progress / done & archived
- **Dependencies**

Determine status from:
- Which files are in `docs/features/` vs. `docs/features/archive/`
- Open `feature/*` branches and open pull requests (`gh pr list`) — in progress
- Whether the corresponding branch has been merged to `develop`

Present as a **table sorted by number**, then a one-line "next up" recommendation
(the lowest-numbered feature that is not started and not in progress).

**Flag the deployability gap explicitly.** Features 04–08 ship permissive — any authenticated
user can edit anything until 09 lands. If the table shows anything from 04–08 merged while 09 is
still not started, say so plainly: `develop` is not safe to deploy in that state. Do not let the
table imply otherwise.
