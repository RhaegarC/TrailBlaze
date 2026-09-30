# Bug Log

Running tracker for reported bugs. One row per bug; the detailed file is
`docs/bugs/NN-name.md`. Resolved bugs move to `docs/bugs/archive/` after the fix PR merges.

Numbering is Project #1 (Mission 1 — TrailBlaze Activity Journal).

| # | Bug (file) | Severity | Component | Reported | Status | Work item |
|---|---|---|---|---|---|---|
| 01 | [01-popup-sign-in-never-completes.md](archive/01-popup-sign-in-never-completes.md) | High | `src/web` — sign-in | 2026-09-28 | archived — merged in PR #34 | `loginIssue` |
| 02 | [02-banner-shows-no-identity.md](archive/02-banner-shows-no-identity.md) | Normal | `src/web` — banner | 2026-09-30 | archived — merged in PR #38 | `fix/banner-identity` |
| 03 | [03-signed-in-list-drops-the-token.md](archive/03-signed-in-list-drops-the-token.md) | High | `src/web` — activity reads | 2026-09-30 | archived — merged in PR #46 | `fix/03-signed-in-list-drops-the-token` |

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
rests on that boundary holding. See the `bug-fix` agent, `~/.claude/agents/bug-fix.md`.

**The mirror of that rule is what is *not* a bug.** A divergence nobody can observe — the code and
the standard disagree, but every caller still sees correct behaviour — does not belong in this log
and does not get a `fix/*` branch: there is no defect to reproduce and no regression test that could
fail. The test is an observable one, which is what lets the two be told apart at all.
