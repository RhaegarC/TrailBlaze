# Technical Debt Log

Running register for known divergences between the code and the standard it is held to. One row
per item; the detail file is `docs/tech-debt/NN-name.md`. Resolved items move to
`docs/tech-debt/archive/` after the fix PR merges.

**This folder is the only debt list.** [STANDARD.md](../../src/api/STANDARD.md) §12 used to hold
one; it now points here. A divergence found anywhere gets filed here — not in the standard, not in a
feature file, and not as a passing note in a PR. A claim recorded in two places is a claim that will
disagree with itself, which is exactly what this register was created to fix: §12.1 asserted the
audit columns were unmaintained for months after the interceptor started maintaining them.

**Numbering is identity, not priority.** Numbers 01–12 are the former §12 items, in §12 order, and
they never change. New items take the next free number — check `archive/` too, since archived
numbers are spent. Reprioritising reorders the rows below and never renames a file; that matters
because STANDARD §12's mapping table, the PRD, feature files and merged PR bodies all link to these
names. Features number by priority because a scan reads the number
(`/next` picks the lowest); nothing scans this folder, so there is nothing to encode.

Items 21–27 arrived together, on 2026-09-18, from one change: running the suite against real
containers. That is not a convention breaking — it is what the change was for. A fake storage
service and an unreachable connection string cannot show you what the engine does, and seven of the
things they were hiding became visible the first time the tests touched a real one. Filing them in
one commit is the honest record of a single afternoon's evidence, and each one carries its own
`Source` line naming the date.

[28](28-soft-deleted-admin-blocks-seeding.md) arrived on 2026-09-19, from verifying feature 03's
seeder against a populated database. It is latent rather than live — nothing in the product deletes a
user yet — and its row says so.

| # | Debt (file) | Kind | Impact | Area | Discharges via | Status |
|---|---|---|---|---|---|---|
| 02 | [`DeleteAsync` does N round trips, untransacted](02-deleteasync-n-round-trips.md) | correctness | silent-wrong | Persistence | — (orphaned) | open |
| 04 | [`UserController` violates the route convention](04-usercontroller-route-convention.md) | correctness | friction | Api | — (orphaned) | open |
| 05 | [A soft delete is recorded as `"Modified"`](05-soft-delete-recorded-as-modified.md) | correctness | silent-wrong | Audit | — (orphaned) | open |
| 07 | [The `.http` file requests `/weatherforecast/`](07-http-file-requests-weatherforecast.md) | hygiene | cosmetic | Api | — (orphaned) | open |
| 11 | [There is no CI or deployment pipeline](11-no-ci-pipeline.md) | capability | blocks | Workflow | — (orphaned) | open |
| 12 | [Feature 02's tests were deferred](12-feature-02-tests-deferred.md) | test-gap | blocks | Tests | 11 (in part) | open (narrowed by 03) |
| 13 | [`DescriptionTooLong` hardcodes `500`](13-description-too-long-hardcoded.md) | hygiene | friction | Model | — (orphaned) | open |
| 14 | [The warning-free build is unenforced](14-warning-free-build-unenforced.md) | process | friction | Build | — (orphaned) | open |
| 15 | [The web app cannot call the API](15-web-app-cannot-call-the-api.md) | capability | blocks | Web | **feature 10** | open |
| 16 | [Unreferenced forward-looking scaffolding](16-unreferenced-scaffolding.md) | hygiene | friction | Persistence | features 06/07/08 (in part) | open |
| 17 | [`AllowedOrigins` is declared twice](17-allowedorigins-duplicated.md) | correctness | silent-wrong | Config | — (orphaned) | open |
| 18 | [Tracked settings carry another repo's permissions](18-claude-settings-cross-repo.md) | hygiene | friction | Workflow | — (orphaned) | open |
| 19 | [Documentation indexes have drifted](19-doc-indexes-drifted.md) | docs | cosmetic | Docs | — (orphaned) | open (partly discharged by 03) |
| 20 | [`LastModifiedOn` is left at its sentinel on insert](20-lastmodified-unset-on-insert.md) | correctness | silent-wrong | Audit | — (orphaned) | open |
| 21 | [The public/private container set exists only in prose](21-container-access-levels-in-prose.md) | capability | silent-wrong | Storage | — (orphaned) | open |
| 22 | [The test tier's database engine is a retired product](22-test-engine-is-a-retired-product.md) | correctness | friction | Tests | — (orphaned) | open |
| 23 | [Feature specs assert foreign keys the model does not have](23-foreign-keys-asserted-that-do-not-exist.md) | docs | friction | Model | features 04/06 (in part) | open |
| 24 | [Every write method returns the entry count, not the rows named](24-write-methods-return-entry-count.md) | correctness | silent-wrong | Persistence | — (orphaned) | open |
| 25 | [`TrailBlaze.Service.Test` has no tests, and no way to host the ones it needs](25-service-test-tier-is-empty.md) | test-gap | friction | Tests | features 06/08 (in part) | open (narrowed by 03) |
| 26 | [The composition root has never opened a connection](26-composition-root-never-opened-a-connection.md) | test-gap | friction | Api | — (orphaned) | open |
| 27 | [`Category!=Container` is not the offline run](27-container-filter-is-not-the-offline-run.md) | docs | cosmetic | Docs | — (orphaned) | open |
| 28 | [A soft-deleted row blocks the configured admin from being seeded](28-soft-deleted-admin-blocks-seeding.md) | correctness | friction | Seeding | — (orphaned) | open |

Rows are ordered by number, which for 01–12 is §12 order and for 13+ is filing order. The queue
order is a judgement, not a column — see "Where to start" at the bottom.

## Kind

What the item *is*, which predicts what repairing it looks like.

| Kind | What it is | The repair motion |
|---|---|---|
| **correctness** | Two sources of truth, a wrong branch, a partial write | Delete or unify one; usually RED-first |
| **capability** | Something the standard or a feature requires that does not exist | Build it — often discharged by the feature that needs it |
| **test-gap** | Behaviour that shipped without the test §10 requires | Write the tests |
| **docs** | A document describing a state the code has left | Correct it, and assert it if it can drift again |
| **hygiene** | Dead code, unreferenced scaffolding, duplicated config | Delete it, or wire it up |
| **process** | A rule this repo states but nothing enforces | Add the enforcement |

## Impact

The cost of leaving it. This drives queue order and how much scrutiny the fix deserves — **not**
which branch it takes, because the answer to that is always the same one.

| Impact | Meaning |
|---|---|
| **silent-wrong** | Wrong today, and nothing fails loudly. These age worst. |
| **blocks** | Stops a feature, or production has no verified path |
| **friction** | Costs time on every change |
| **cosmetic** | Docs, indexes, a dead request line |

**Debt never takes `hotfix/`.** There is no debt emergency, and `bug-fix`'s hotfix path already owns
the case where there is one. If an item is failing *now* — or if anything would let private media or
an Entra object id reach the wrong caller — it is not debt, it is a bug: file it with
`/capture bug` and fix it with the `bug-fix` agent, where a privacy failure is Critical by default.

The boundary is observable rather than a matter of taste.
[Item 01](archive/01-audit-columns-have-two-writers.md) wrote the same field twice and the
**correct** writer won, so nothing a caller saw was wrong — that is the definition of an item that
belongs here, and it is the register's worked example. Had the dead write been the one that won, it
would have been a bug. When you cannot tell which of the two you have, you have a bug.

## Discharges via

An item is somebody's problem, and this column names who. It exists because the alternative is a
register that quietly races the feature work already fixing the same code.

- **`feature NN`** — that feature's PR is expected to close this. **Do not fix it independently**;
  that branch is already changing that code, and the two will conflict for no reason.
- **another debt number** (`01`) — this item is a *facet* of that one, not a second repair. Fix 01
  and this closes with it, even when the facet needs no edit of its own:
  [item 06](archive/06-timestamp-types-inconsistent.md) closed exactly that way, discharged by 01
  because the inconsistency lived inside the lines 01 deleted.
- **`— (orphaned)`** — nothing claims it. These are the ones that need an owner, and they are the
  reason this register exists.

`debt-fix` treats this column as a **hard gate**: if it names a feature or a debt item that is not
yet merged, the agent stops rather than starting work that is already in flight. Taking an item over
deliberately means clearing the field in a doc commit that says why the owner will not discharge it
— an explicit decision, recorded, not a silent override.

## Testability

Every item file declares one of three, and the difference is visible in how it closes:

| Value | Meaning | How it closes |
|---|---|---|
| **testable** | A behaviour exists | RED → GREEN, the ordinary path |
| **doc-assertion** | No behaviour, but a durable invariant in a file | A test reads the file and asserts the invariant; confirm it goes red when reverted. Sanctioned in [STANDARD.md](../../src/api/STANDARD.md) §10 |
| **verification-only** | Genuinely unassertable | A mandatory `Verification:` line in the close checklist, and a "No test — and why" section in the PR body. Nothing is invented to look like coverage |

A `verification-only` close is a **weaker claim** than a tested one, and the log marks it rather than
letting the two read alike — the same reasoning as the storage tier skipping rather than
disappearing, and `sprint-status` warning that "all tests passing" is weaker than it sounds.

## Archive

| # | Item | Resolved | By |
|---|---|---|---|
| 01 | [Audit columns had two writers](archive/01-audit-columns-have-two-writers.md) | 2026-09-17 | PR #8 |
| 03 | [Migrations were Npgsql-shaped](archive/03-migrations-npgsql-shaped.md) | feature 01 | PR #3 |
| 06 | [Timestamp types were inconsistent](archive/06-timestamp-types-inconsistent.md) | with item 01, no separate change | PR #8 |
| 08 | [`SampleTemplate/placeholder.txt`](archive/08-sampletemplate-placeholder.md) | upstream template | — |
| 09 | [`DbConnection` was accepted empty](archive/09-dbconnection-accepted-empty.md) | feature 01 | PR #3 |
| 10 | [Namespaces were block-scoped](archive/10-block-scoped-namespaces.md) | 2026-09-16 | — |

## Not debt — do not file these

| Thing | Why it is not here |
|---|---|
| The activity and media tables do not exist | Planned work with a home — [feature 04](../features/04-activity-crud.md) |
| Everything in [docs/features/backlog.md](../features/backlog.md) | Deliberately deferred ideas, not divergences |
| The PRD's "Out of scope / deferred" list | Decided against, not deferred |
| `AGENTS.md` being byte-identical to `CLAUDE.md` | Deliberate dual-harness parity. Only the absence of a check is debt, and that is [19](19-doc-indexes-drifted.md) |
| A rule you find inconvenient | That is a proposal to change the standard, which is a PR against STANDARD.md — not debt |

## Where to start

Impact first, then whoever is closest to the code already. The four that repay a first hour most:
**[20](20-lastmodified-unset-on-insert.md)** (silent-wrong, one assertion and one branch, and it
forces a schema decision worth making deliberately), **[05](05-soft-delete-recorded-as-modified.md)**
(silent-wrong, and it is the one item whose cost grows every day it stays open — it can only be fixed
without a backfill while no history exists), **[13](13-description-too-long-hardcoded.md)**
(friction, one line, testable), and **[17](17-allowedorigins-duplicated.md)** (silent-wrong, and the
duplication already misbehaves between profiles). **[11](11-no-ci-pipeline.md)** blocks the most and
is the largest.

Item [01](archive/01-audit-columns-have-two-writers.md) and its facet
[06](archive/06-timestamp-types-inconsistent.md) were the first two off this list, closed by PR #8.

Of the seven filed on 2026-09-18, two are silent-wrong and cheap:
**[24](24-write-methods-return-entry-count.md)** (one helper, and the container tier already measures
the wrong number) and **[21](21-container-access-levels-in-prose.md)** (a startup assertion; today
the rule that `covers` and `avatars` are public and `media` is private exists only in a doc comment,
so nothing in a deployment sets it). The other
five do not repay an hour: [22](22-test-engine-is-a-retired-product.md) and
[25](25-service-test-tier-is-empty.md) change how much the suite is worth rather than fixing
anything now, and [23](23-foreign-keys-asserted-that-do-not-exist.md),
[26](26-composition-root-never-opened-a-connection.md) and
[27](27-container-filter-is-not-the-offline-run.md) are documentation and shape debt.
**[28](28-soft-deleted-admin-blocks-seeding.md)** is the same shape: real, verified, and unreachable
until something deletes a user, which no feature does yet.
