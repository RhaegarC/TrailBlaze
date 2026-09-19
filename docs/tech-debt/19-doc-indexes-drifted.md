# 19 — Documentation indexes have drifted from what they index

Status: **open (partly discharged by feature 03)** · Kind: docs · Impact: cosmetic · Area: Docs
Source: found 2026-09-17 (not a §12 item) · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-19

## Update — 2026-09-19 (feature 03)

**The test count now has one home, which is the durable half of this item's argument.** The number
was restated in five documents — README, [testing-and-tdd.md](../testing-and-tdd.md), the PRD's
current-state table, the sprint file and STANDARD §10 — and every addition of a test meant five hand
edits. The count moved **31 → 42 → 59 → 76 → 70 in three days**, which is the drift this item
predicted when it said the fix that lasts is the one that stops the number mattering.

So that fix was made rather than the number corrected a fourth time:

| Document | Before | Now |
|---|---|---|
| [testing-and-tdd.md](../testing-and-tdd.md) | one of five copies | **the only copy** — and it says so, so the next editor stops there |
| [README.md](../../README.md) | "discovers 76 tests … 38 pass and 38 skip" | states which tiers run when, and links |
| [PRD.md](../PRD.md) current-state table | same counts | states the container property, points at the strategy doc |
| [00-mission-1-sprint.md](../features/00-mission-1-sprint.md) | same counts, twice | the DoD line points at the strategy doc |
| [STANDARD.md](../../src/api/STANDARD.md) §10 | same counts, with a per-project split | keeps the *claim* (which tier runs when, and that the skips are the database tier only) and says explicitly that no count belongs there |

**The distinction that makes this correct rather than a deletion.** Dated measurements stay — a
"Checked 2026-09-18" line in this register is a record of what was observed then, and rewriting it
would be falsifying evidence. What moved is the *live* claim: the number a reader consults to check
today's run. [Item 27](27-container-filter-is-not-the-offline-run.md)'s dated table keeps its counts
for the same reason, and its 2026-09-19 note was rewritten to stop asserting new ones.

**This item does not close on that.** Step 3 below — the command-list `doc-assertion`, which is the
one part that catches drift rather than preventing it — was not written.

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

**Status as of 2026-09-18: four of the six are fixed, and two are not.**

| Instances | State |
|---|---|
| the decision count, the command list, the Start-here table, the test count | **fixed** — corrected in the PR that ran the suite against containers, which touched README for other reasons |
| the user-secrets sentence | **open**, and owned by item [17](17-allowedorigins-duplicated.md) |
| §13's template job | **open**, and owned by item [11](11-no-ci-pipeline.md) |

This item is therefore no longer "the README has drifted" so much as "nothing keeps it from drifting
again", which is the `doc-assertion` in the Testability section and the reason it stays open after
its last instance is fixed.

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
self-defeating. Two of these five were things the register's own PR changed the world for (the new
`docs/tech-debt/` folder, the seventh command's doc), and fixing them was part of that change rather
than a follow-up — which is why four of the six are now closed.

**What survives the fixes is the pattern.** The test-count instance is the clearest case: it was
corrected twice within two days, by two different changes, and both corrections were manual. A
document that has to be re-edited whenever an unrelated PR adds a test is a document that will be
wrong again; the fix that lasts is the one that stops the number mattering.

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
- **Re-measured 2026-09-18, against containers:** **59 discovered** — 55 in
  `TrailBlaze.Repository.Test` (16 offline + 11 storage + 28 database), 4 in `TrailBlaze.Api.Test`,
  0 in `TrailBlaze.Service.Test`. Containers up: **59 passed**. Nothing configured: **31 passed,
  28 skipped**. So the number has roughly doubled since this item was opened, which is the point
  below rather than a detail — see [testing-and-tdd.md](../testing-and-tdd.md) for the tier split.
- **The attribute count is not the test count, which is what made this look uncertain.** There were
  **32** `[Fact]`/`[Theory]` attributes across the three projects and **42** tests at runtime, because
  theories expand — 31 was close enough to 32 to look like a plausible older value rather than a
  stale one. Anyone re-checking a count here should read the runner's total, never the attribute
  count.
- **A count in a document is stale the moment a test is added, which is why this item keeps
  reopening.** The number moved 31 → 42 → 59 while the item was open, and each move required the
  same hand edit in README, the strategy doc and here. That is the argument for asserting the
  *claim* (which tier runs when) rather than the number, and it is why the repair plan's step 4 is
  the weakest of the five.

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

1. ~~**Fix the four README items and §13.**~~ **Done 2026-09-18** for the decision count, the command
   list, the Start-here table and the test count. §13 is item [11](11-no-ci-pipeline.md)'s to correct
   once a pipeline exists; the user-secrets sentence is item [17](17-allowedorigins-duplicated.md)'s,
   awaiting the config decision.
2. ~~**Add the `docs/tech-debt/` row** to the Start-here table.~~ **Done** — the row exists at
   [README.md:16](../../README.md#L16).
3. **Write the command-list `doc-assertion`.** Still the item's durable half, and now the only part
   of it that outlives the next test.
4. ~~**Settle the test count** by running `dotnet test`.~~ **Done, and it did not stay settled** —
   the number went 42 → 59 → 76. Recording it was still right; treating it as the deliverable was
   not, and the count is now written in one document only (see the update above).
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

- [x] README's decision count, command list, Start-here table and test count are accurate (2026-09-18)
- [ ] README's user-secrets sentence is accurate, or item [17](17-allowedorigins-duplicated.md) says why not
- [ ] §13's "CI's template job" line corrected or removed
- [ ] A `doc-assertion` test fails when a command is added and README is not updated
- [ ] The test count was settled by running `dotnet test`, and the number in README matches
- [ ] `CLAUDE.md`/`AGENTS.md` counts refreshed by `analyze`, in their own commit
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
