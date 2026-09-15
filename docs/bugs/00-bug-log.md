# Bug Log

Running tracker for reported bugs. One row per bug; the detailed file is
`docs/bugs/NN-name.md`. Resolved bugs move to `docs/bugs/archive/` after the fix PR merges.

Numbering is Project #1 (Mission 1 — TrailBlaze Activity Journal).

| # | Bug (file) | Severity | Component | Reported | Status | Work item |
|---|---|---|---|---|---|---|
| — | *(no bugs reported yet)* | | | | | |

## Severity guide

| Severity | Meaning | Path |
|---|---|---|
| **Critical** | Production down, or **private media reachable without authorization** | `hotfix/*` off `master` |
| **High** | Major function broken; no data exposure | `fix/*` off `develop` |
| **Normal** | Incorrect behaviour with a workaround | `fix/*` off `develop` |
| **Low** | Cosmetic or minor | `fix/*` off `develop` |

**Privacy failures are critical by default.** Any bug where an activity's images or videos are
reachable without authentication, or where a SAS URL is issued to an unauthorized caller, is
Critical regardless of how small the repro appears — the whole public-text/private-media split
rests on that boundary holding. See [.claude/agents/bug-fix.md](../../.claude/agents/bug-fix.md).
