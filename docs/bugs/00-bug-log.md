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

**The mirror of that rule is the tech-debt register.** A divergence nobody can observe — the code
and the standard disagree, but every caller still sees correct behaviour — is **debt, not a bug**,
and it does not belong in this log or on a `fix/*` branch: file it with `/capture debt` at
[docs/tech-debt/00-debt-log.md](../tech-debt/00-debt-log.md). The register states the boundary as an
observable test, which is why the two lists can be told apart at all — and why filing the same
defect in both is a mistake rather than caution.
