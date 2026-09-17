# 09 — `DbConnection` was accepted empty

Status: **Archived** — resolved by [feature 01](../../features/archive/01-foundation.md), merged to `develop` in PR #3 · [00-debt-log.md](../00-debt-log.md)
Source: STANDARD §12.9 · Opened: 2026-09-15 · Archived: 2026-09-17

## What it was

Configuration binding accepted an empty `DbConnection`. The host started, the connection string was
empty, and the failure surfaced later — at the first query, as a connection error that named
nothing about the missing setting.

STANDARD §9 had recorded two cases as deliberately open, both resting on that leniency: that the host
"runs for `/health` without a database". Those cases depended on an unset connection string being
tolerable, which is a property nobody had decided on purpose.

## How it resolved

Feature 01 introduced `RequireSetting`, and the composition root now requires three keys —
`DbConnection`, `BlobConnection` and `AllowedOrigins` — **failing startup with a message naming the
key that is missing**. The two open cases in §9 no longer hold, and §9 carries a corrected note.

The README documents the consequence for a developer: there is no zero-configuration boot, and the
three values come from user-secrets for a local run.

## Lesson

**A start-up that succeeds with unusable configuration converts a configuration error into a runtime
error somewhere else.** The cost is not the delay; it is that the eventual error names the wrong
thing, so the reader debugs the query instead of the setting. Requiring the key moves the failure to
the moment the information is still available — the host knows which key is missing, and a query
does not.

The secondary lesson is about the §9 note itself: "left open" was recorded as a decision when it was
really an unexamined consequence. §9 is a better document now that it describes what actually
happens — which is the same repair §12.1 needed.

## Why archived rather than deleted

Every current and future deployment depends on the fail-fast rule, and the rule's reason is a failure
mode rather than a preference. Kept so the reason travels with it — and because the README's
"no zero-configuration boot" statement is a consequence of this item rather than an independent
choice.
