# 22 — The test tier's database engine is a retired product

Status: **open** · Kind: correctness · Impact: friction · Area: Tests
Source: found 2026-09-18 (during the container-backed test change) · Discharges via: — (orphaned) · Opened: 2026-09-18 · Last verified: 2026-09-18

## What the debt is

The database tier runs against `mcr.microsoft.com/azure-sql-edge`, and that product has been
**retired**: Microsoft has stopped shipping it and issues no further servicing for it. The image this
repo pins reports itself as an **RTM** build, which is the visible symptom — there is no servicing
line above it to move to:

```
Microsoft Azure SQL Edge Developer (RTM) - 16.0.5100.7245 (X64)
```

The `(RTM)` marker is the point. A supported engine reports a cumulative update; this one reports the
release-to-manufacturing build, so it is the first and only build of that version anyone will get.

## Why it matters

**The tests now execute against an engine that the deployed database is not.** The deployed target is
Azure SQL Database, which is continuously serviced; the test tier is a frozen RTM build. The two
agreed closely enough on 2026-09-18 for every assertion in the tier to hold — that is not the claim
in doubt. What is in doubt is that they will keep agreeing, and **nothing will notice when they
stop**. A behaviour change, a new error number, a stricter parser: all of it arrives in Azure SQL
Database and none of it in the test tier, so the tier would keep reporting green while describing an
engine the product no longer runs on.

**This is a real but slow cost, which is why the impact is `friction` and not `silent-wrong`.**
Nothing is wrong today. Every assertion in the tier was measured against this engine and the numbers
are recorded where they were used — 2628 for a truncation, 2627 for a duplicate key, compatibility
level 160. Those were *measured*, not assumed, which is the discipline that makes this item
survivable: the day the engine is replaced, the numbers get re-measured rather than re-guessed.

**The container image itself is also outside the deployment's security posture.** It runs on a
developer machine or a CI runner and holds nothing but test data, and it is bound to `127.0.0.1` by
`docker-compose.test.yml`. The exposure is real but small, and it is stated here so the item is
complete rather than to inflate it.

## Evidence

Checked 2026-09-18.

- `SELECT @@VERSION` on the running container returns
  `Microsoft Azure SQL Edge Developer (RTM) - 16.0.5100.7245 (X64)`.
- `SELECT compatibility_level FROM sys.databases WHERE name = 'model'` returns **160**, which is why
  the tier's truncation errors are `2628` rather than the legacy `8152` both are asserted as.
- `docker-compose.test.yml` pins the image tag, so a rebuild does not silently move the engine.
- The README already calls out the standing asymmetry that matters here: Azure SQL Database is
  managed and has no local stand-in. This item is the consequence of the *test tier* having one.

## Testability

**verification-only.** The property is "the engine the tests run against still agrees with the engine
the product runs against", and that is not a claim any test in this repository can make — a test
asserting it would be running on the engine whose agreement is in question. It closes by checking the
candidate replacement against the tier and recording what changes.

The tier does, however, make the *switch* cheap and checkable, and that is worth stating: every
engine-specific number is already asserted in a named constant at the point of use
(`TruncationErrors`, `SqlFailures.DuplicateKey`), so a replacement fails loudly rather than passing
vacuously wherever the numbers moved.

## Repair plan

1. **Pick the replacement, and ask the replicas/deployment question of it.** The candidates differ in
  kind, not just in version:
   - *(a)* **SQL Server 2022 or 2025 in a Linux container** — the closest engine to Azure SQL
     Database that can run locally, and still serviced. Costs image size and startup time, and the
     licence for a non-Developer edition is a real question.
   - *(b)* **Azure SQL Database itself, in a throwaway database per run** — faithful by
     construction, credentialed, slower, and it makes the tier network-dependent. It also removes the
     skip-on-a-bare-machine property this change deliberately added.
   - *(c)* **Keep SQL Edge, time-boxed** — defensible only with a written expiry and a named trigger,
     since "no further patches" will not improve.
2. Re-run the tier against the candidate and **re-measure every engine-specific constant**. The
   truncation set, the duplicate-key set, and the compatibility level are the three that exist today.
3. Decide the skip semantics for the new engine; if it needs credentials, item 11's CI question
   changes shape again.
4. Record the decision in the PRD's deployment section, which is where the "real cloud resource in
   every environment" asymmetry is documented.

**Recommendation: (a),** and treat it as a scheduled replacement rather than a fix. It keeps the
tier credential-free and offline-capable — the two properties that make it run on every machine —
while restoring a serviced engine. Revisit (b) only if the product's engine-specific behaviour
becomes load-bearing enough that "closest available engine" is not good enough.

## Out of scope / related

- **The skip boundary is not in question.** Whatever engine replaces this one, an unreachable
  container must still skip rather than fail.
- **[Item 11](11-no-ci-pipeline.md)** owns whether CI runs the tier at all; a credentialed
  replacement would change that item's answer.
- **The fluent bounds and migration assertions are engine-independent** and survive any replacement.
  The engine-specific ones are listed in the repair plan so the blast radius is not guessed.

## Close checklist

- [ ] The pinned image runs an engine that still receives servicing
- [ ] Every engine-specific constant has been re-measured and updated, with the measurement recorded
- [ ] The tier is green against the replacement, container up and container down
- [ ] The PRD's deployment section states which engine the tests use and why
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
