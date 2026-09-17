# 19 — Documentation indexes have drifted from what they index

Status: **open** · Kind: docs · Impact: cosmetic · Area: Docs
Source: found 2026-09-17 (not a §12 item) · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

The documents that *index* other documents no longer describe them. Five verified instances:

| Where | Says | Actually |
|---|---|---|
| [README.md:13](../../README.md#L13) | PRD has "25 logged decisions" | the PRD carries **Decision #30** |
| [README.md:35-41](../../README.md#L35-L41) | six commands, listed as the workflow | there are **seven** — `/list-features` exists at [.claude/commands/list-features.md](../../.claude/commands/list-features.md) and is absent from the list |
| [README.md:47-49](../../README.md#L47-L49) | required settings come from user-secrets, "never from `appsettings.json` or `launchSettings.json`" | `AllowedOrigins` is in **both** — see item [17](17-allowedorigins-duplicated.md) |
| [README.md:86](../../README.md#L86) | the suite "runs 31 tests across the three tiers, 30 of them by default" | `dotnet test` reports **42 runnable, 41 passing, 1 skipped** |
| [README.md:11-16](../../README.md#L11-L16) | the "Start here" table | has no row for `docs/tech-debt/`, which this register creates |
| [STANDARD.md](../../src/api/STANDARD.md) §13 | "CI's template job stays green" | there is no CI at all, and no template job — item [11](11-no-ci-pipeline.md) |

One more is auto-generated and cannot be fixed by hand: `CLAUDE.md` and `AGENTS.md` state the index
as "520 symbols, 983 relationships, 15 execution flows", while the 2026-09-17 re-index reports
**890 nodes, 1,854 edges, 32 flows**. Those counts sit between `<!-- gitnexus:start -->` markers that
`analyze` rewrites, so a hand edit is overwritten — refreshing them needs `analyze` *without*
`--index-only`, which is a different command from the one this register's work ran.

## Why it matters

**An index is read by people who have not yet checked anything.** README's Start-here table is the
first thing a new reader trusts, and its command list is how they learn the workflow — one that omits
a command is worse than one that lists none, because it looks complete. The 25-versus-30 decision
count is the same failure as §12.1 in miniature: a number in a trusted document that nothing checks.

**The README's user-secrets sentence is the one that is load-bearing**, which is why item 17 owns it
rather than this item. It is cited here because it is the clearest example of the pattern: the
document states a rule the code does not follow, and a reader who believes it will look in the wrong
place for a setting that is in the repository.

**This register was created to cure exactly this**, so leaving the indexes stale on day one would be
self-defeating. Two of these five are things this PR changes the world for (the new `docs/tech-debt/`
folder, the seventh command's doc), and fixing them is part of the change rather than a follow-up.

## Evidence

Checked against each file 2026-09-17.

- `grep -oE 'Decision #[0-9]+' docs/PRD.md | sort -u -V | tail -3` → #28, #29, **#30**.
- `ls .claude/commands/` → seven files including `list-features.md`; README lists six.
- README:47-49 read in full, beside `launchSettings.json:20` and `appsettings.Development.json:8`.
- The re-index output of 2026-09-17 recorded in this session, against `CLAUDE.md`'s header counts.
- **The test count was settled by running the suite, 2026-09-17:** `dotnet test` reports **42
  runnable, 41 passing, 1 skipped** — Service 10, Repository 27 passing + 1 skipped, Api 4. README's
  "31 … 30 by default" is stale by eleven. [testing-and-tdd.md:20-22](../testing-and-tdd.md) already
  had it right at "42 runnable … leaving 41 passing by default", so the README is the document that
  drifted, not the strategy doc. Item [12](12-feature-02-tests-deferred.md)'s "41 tests, 40 passing"
  was off by one and is corrected in the same pass.
- **The attribute count is not the test count, which is what made this look uncertain.** There are
  **32** `[Fact]`/`[Theory]` attributes across the three projects and **42** tests at runtime, because
  theories expand — 31 was close enough to 32 to look like a plausible older value rather than a
  stale one. Anyone re-checking a count here should read the runner's total, never the attribute
  count.

## Testability

**doc-assertion** for the parts that are file contents, **verification-only** for the auto-generated
counts.

The `doc-assertion` worth writing here is narrow and valuable: assert that every file in
`.claude/commands/` is named in README's command list. That fails the day a command is added and the
list is not — the exact way `/list-features` went missing — and it needs no network, no build, and no
database.

The decision count and the test count are not worth asserting; they change for good reasons and a
test would be a chore with no failure worth catching. The index counts in `CLAUDE.md` cannot be
asserted either, being generated.

## Repair plan

1. **Fix the four README items and §13.** In the same pass as item 17 for the user-secrets sentence,
   since that repair decides which way it reads.
2. **Add the `docs/tech-debt/` row** to the Start-here table — part of this PR, not a follow-up, since
   the table is the register's entry point.
3. **Write the command-list `doc-assertion`.**
4. **Settle the test count** by running `dotnet test` and writing what it reports.
5. **Refresh `CLAUDE.md`/`AGENTS.md` counts** by running `analyze` without `--index-only`, on its own
   commit, and never by editing inside the markers.

## Out of scope / related

- **Item [17](17-allowedorigins-duplicated.md) owns the README user-secrets sentence.** This item
  lists it as an instance of the pattern; the fix belongs with the config decision.
- **The numbered-doc sections are not in scope.** §12 is being repointed by this register's own PR;
  the rest of STANDARD's numbering was checked and is consistent.
- **`docs/features/backlog.md` and `docs/testing-and-tdd.md` were not found to have drifted**, design
  and test-tier content being less countable than an index. Their absence from this item is a
  checked result, not an omission.
- **A link checker is not proposed.** The relative-link mistakes this project has made were made
  once, were caught in review, and a checker needs a runner — which is item
  [11](11-no-ci-pipeline.md). Revisit after 11.

## Close checklist

- [ ] README's decision count, command list, Start-here table and user-secrets sentence all accurate
- [ ] §13's "CI's template job" line corrected or removed
- [ ] A `doc-assertion` test fails when a command is added and README is not updated
- [ ] The test count was settled by running `dotnet test`, and the number in README matches
- [ ] `CLAUDE.md`/`AGENTS.md` counts refreshed by `analyze`, in their own commit
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
