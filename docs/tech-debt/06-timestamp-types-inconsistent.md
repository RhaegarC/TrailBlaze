# 06 — Timestamp types are inconsistent between the model and the repository

Status: **open** · Kind: correctness · Impact: silent-wrong · Area: Audit
Source: STANDARD §12.6 · Discharges via: **01** · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

The audit columns are `DateTimeOffset` and the repository writes a `DateTime` into them.

- `EntityBase.CreatedOn` / `LastModifiedOn` are `DateTimeOffset`
  ([EntityBase.cs:15,19](../../src/api/TrailBlaze.Model/DatabaseEntities/EntityBase.cs#L15)).
- `AuditLog.Timestamp` is `DateTimeOffset`
  ([AuditLog.cs:63](../../src/api/TrailBlaze.Model/DatabaseEntities/AuditLog.cs#L63)).
- `DatabaseRepository.DeleteAsync` writes `item.LastModifiedOn = DateTime.UtcNow`
  ([DatabaseRepository.cs:81](../../src/api/TrailBlaze.Repository/DatabaseRepository.cs#L81)) — a
  `DateTime`, implicitly converted.

## Why it matters

**The immediate consequence is nil, and that is the whole problem.** The interceptor overwrites the
value with `DateTimeOffset.UtcNow` on the same save, so the divergent write never reaches the
database. There is no wrong data, no failing test, and no symptom.

What is left is a source-level inconsistency that a reader has to reason about to dismiss:
`DateTime.UtcNow` into a `DateTimeOffset` column looks like a bug, is not one today, and becomes one
the moment the overwrite stops happening. Leaving it means every future reader of this file repeats
the analysis, and the file misleads about the type discipline the standard asks for.

**This is a facet of item [01](01-audit-columns-have-two-writers.md), not a second repair.** The
inconsistency exists only inside the dead writes that item deletes. Fix 01 and the type mismatch
disappears with no separate change.

It keeps its own number because STANDARD §12.6 was a numbered item and the §12 → NN mapping must
land every one of them somewhere; a number that resolves to nothing is worse than a gap.

## Evidence

Checked against the code 2026-09-17.

- Column declarations and the divergent write as quoted above.
- **STANDARD contradicts itself on this exact point**: §3's Timestamps says the `EntityBase` columns
  "are `DateTime` and are **not yet maintained**"
  ([STANDARD.md:255-259](../../src/api/STANDARD.md#L255-L259)), while §12.6 of the same document says
  they are `DateTimeOffset`. §3 is wrong on both counts — the type and the maintenance. Correcting it
  is item [19](19-doc-indexes-drifted.md)'s territory and is required either way.

## Testability

**testable**, but only as a *type-level* assertion, so state the limit honestly: the model metadata
can be read offline (the repository tier already asserts column shapes against `IModel`), but the
absence of a `DateTime` write cannot be asserted by a behavioural test, because the interceptor makes
the two indistinguishable at the database.

The practical test is therefore the one item [01](01-audit-columns-have-two-writers.md) produces: no
`DateTime.UtcNow` assignment remains in the repository. That is closer to a `doc-assertion` than a
behavioural test, and it should not be dressed up as more.

## Repair plan

1. **Do item [01](01-audit-columns-have-two-writers.md) first.** If this item is picked up alone, the
   first step is to re-verify that the writes still exist — if 01 has landed, this closes as
   resolved-with-no-change rather than being "fixed".
2. Delete the `DateTime.UtcNow` write along with its sibling (01's repair plan, step 2).
3. Grep the repository layer for any other `DateTime.UtcNow` written into a `DateTimeOffset` member
   and fix or file what it finds. The expected answer is none, but the grep is the evidence.
4. Correct STANDARD §3's Timestamps (shared with [19](19-doc-indexes-drifted.md)).

## Out of scope / related

- **The design question underneath** is whether `DateTimeOffset` is right for audit columns at all.
  STANDARD §3 says "prefer `DateTimeOffset` for anything new", which is a preference, not a rule, and
  the PRD's data model declares `datetimeoffset`. Settled; not this item.
- **`DateTime.UtcNow` elsewhere** is a different matter and is not covered by this item — the audit
  interceptor uses `DateTimeOffset.UtcNow` correctly, and `DatabaseRepository` is the only divergent
  writer found. If the grep in step 3 finds more, file them rather than widening this item.

## Close checklist

- [ ] Item [01](01-audit-columns-have-two-writers.md) landed, or this re-verified as already resolved
- [ ] No `DateTime` write to a `DateTimeOffset` member remains in `TrailBlaze.Repository`
- [ ] The grep result recorded in the PR body, including "none found" if that is the answer
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
