# 06 — Timestamp types were inconsistent between the model and the repository

Status: **Archived** — resolved with item [01](01-audit-columns-have-two-writers.md), with no change of its own · [00-debt-log.md](../00-debt-log.md)
Source: STANDARD §12.6 · Opened: 2026-09-17 · Archived: 2026-09-18

## What it was

The audit columns are `DateTimeOffset`, and one repository method wrote a `DateTime` into them:

- `EntityBase.CreatedOn` and `EntityBase.LastModifiedOn` are `DateTimeOffset`.
- `AuditLog.Timestamp` is `DateTimeOffset`.
- `DatabaseRepository.DeleteAsync` wrote `item.LastModifiedOn = DateTime.UtcNow` — a `DateTime`,
  implicitly converted.

**The immediate consequence was nil, and that was the whole problem.** The interceptor overwrote the
value with `DateTimeOffset.UtcNow` on the same save, so the divergent write never reached the
database: no wrong data, no failing test, no symptom. What remained was a source-level inconsistency
a reader had to reason about in order to dismiss — `DateTime.UtcNow` into a `DateTimeOffset` column
looks like a bug, is not one today, and becomes one the moment the overwrite stops happening.

## How it resolved

**Closed with item [01](01-audit-columns-have-two-writers.md), as resolved-with-no-change.** The
inconsistency existed only inside the dead writes that item deleted — the `DateTime.UtcNow`
assignment and its `"sys"` neighbour were the same two lines — so fixing 01 made it disappear with no
separate edit. This is what the item predicted when it was filed, which is what `Discharges via: 01`
recorded.

The evidence the close asked for, gathered 2026-09-17:

- `grep -rn "[^f]DateTime\.UtcNow\|[^f]DateTime\.Now" src/api/` → **none found**, anywhere in the API.
  Not merely none in `TrailBlaze.Repository`: no divergent timestamp write of any kind remains.

No test was written, and this item never called for one. The absence of a `DateTime` write cannot be
asserted behaviourally, because at the database the interceptor makes the two indistinguishable —
which is the same property that made the debt invisible.

## Lesson

**A facet is worth its own number even when it has no repair of its own.** This item never needed a
separate fix, and filing it separately is what made that legible: a reader who reaches it learns the
type mismatch existed, that it was inert, and that item 01 closed it — rather than rediscovering the
analysis from the code.

It keeps its number because STANDARD §12.6 was a numbered item and the §12 → NN mapping must land
every one of them somewhere. A number that resolves to nothing is worse than a gap.
