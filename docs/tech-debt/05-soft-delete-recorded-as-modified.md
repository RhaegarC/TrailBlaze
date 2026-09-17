# 05 — A soft delete is recorded as `"Modified"`, indistinguishable from an update

Status: **open** · Kind: correctness · Impact: silent-wrong · Area: Audit
Source: STANDARD §12.5 · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

The audit log's `Action` column holds the EF `EntityState` verbatim —
[AuditSaveChangesInterceptor.cs:124](../../src/api/TrailBlaze.Repository/AuditSaveChangesInterceptor.cs#L124):

```csharp
Action = entry.State.ToString(),
```

A soft delete sets `IsDeleted = true` and calls `Context.Update(item)`, which makes the state
`Modified` — not `Deleted`
([DatabaseRepository.cs:84-85](../../src/api/TrailBlaze.Repository/DatabaseRepository.cs#L84-L85),
two lines lower than it was before item [01](01-audit-columns-have-two-writers.md) landed).
So the history records `"Modified"` for a delete.

The entity's own XML documentation states the behaviour as fact rather than flagging it —
[AuditLog.cs:39-42](../../src/api/TrailBlaze.Model/DatabaseEntities/AuditLog.cs#L39-L42) notes that a
soft delete's vocabulary is recorded as `Modified`.

## Why it matters

**The audit trail cannot answer the question it exists to answer.** `AuditLog` is append-only by
design and is the record of what happened; "which entries were deleted, and by whom" is among the
first things anyone asks of it. Today that query returns nothing, and the information is not merely
misnamed — it is *absent*, because `"Modified"` also describes every ordinary update.

The distinguishing data does survive inside `NewValues`: the serialized entity carries
`IsDeleted: true`, so a determined reader can find deletes by parsing JSON. That is the honest
description of the current state — not "deletes are unrecorded" but "deletes are recorded in a place
that requires deserializing an audit blob to find, while the column designed to name the action says
something else."

It ages badly in a specific way: the longer it runs, the more history exists that a fix cannot
retroactively correct. Every day this stays open adds rows that will permanently read `"Modified"`.
That is an argument for fixing it before the first real deployment rather than after.

**Nothing fails, and nothing a caller sees is wrong** — which is the debt test. The data loss is in
the history, and the history is only consulted after something has already gone wrong.

## Evidence

Checked against the code 2026-09-17.

- `Action = entry.State.ToString()` at line 124; the `Modified` case that produces the state for a
  soft delete is the same `Context.Update(item)` item [01](01-audit-columns-have-two-writers.md) is
  about, though for a different reason.
- `SerializeEntity` writes `null` for `newValues` only when the state is `Deleted`
  ([:116-118](../../src/api/TrailBlaze.Repository/AuditSaveChangesInterceptor.cs#L116-L118)) — so
  because the state is `Modified`, both old and new values are captured, and `IsDeleted: true` is
  recoverable from `NewValues`.
- `DeleteAsync<T>` still has no production caller (grepped, 2026-09-17), so no `"Modified"` row in any
  database yet misrepresents a delete. **The window to fix this without a backfill is now.**

## Testability

**testable** at the repository tier with no database, and the existing suite already has the shape:
`A_modified_entity_is_recorded_as_modified_and_stamped` (`AuditTests.cs:180-195`) asserts on the
`Action` value today. The new test asserts that a soft-deleted entity is recorded with a distinct
action — it should fail against the current interceptor and pass after the fix.

## Repair plan

1. **Decide the vocabulary before touching the code**, because the audit table is append-only and
   the values are permanent. `"Deleted"` is the obvious choice, but STANDARD §5 says `Action` holds
   the EF `EntityState`, and the section's "`Actor` — three distinct values, not one 'unknown'"
   discussion is the precedent for how this project reasons about enumerating an audit vocabulary.
   Fix the standard in the same PR if the rule changes.
2. RED: a test asserting a soft-deleted entity's audit row is distinguishable from an update's.
3. GREEN: derive the action rather than copying the state — if `IsDeleted` is set on a `Modified`
   entry, record `"Deleted"`. Do **not** switch the entry to `EntityState.Deleted`: that is a hard
   delete and would destroy the row.
4. Update [AuditLog.cs:39-42](../../src/api/TrailBlaze.Model/DatabaseEntities/AuditLog.cs#L39-L42),
   whose XML doc currently documents the defect as intended behaviour.
5. Update STANDARD §5's "What is recorded" and the note on `Actor` if the action vocabulary is now
   explicitly enumerated there.

## Out of scope / related

- **Item [01](01-audit-columns-have-two-writers.md) is not a prerequisite.** Deleting the dead
  audit writes there does not change the entity state, so a soft delete still records `"Modified"`
  until this item is fixed. They can land in either order.
- **A backfill is deliberately not proposed.** If this ships after any real data exists, the rows
  written before it are unrecoverable by a schema change — recovering them means scanning
  `NewValues` JSON. The repair plan's job is to land before that, and the PR should say so.
- **Hard deletes are already distinct**: the interceptor maps `EntityState.Deleted` to `"Deleted"`
  today. This item is only about the soft path.

## Close checklist

- [ ] A test asserting a soft delete is distinguishable from an update in the audit log, red first
- [ ] The chosen action vocabulary recorded in STANDARD §5, not just in the code
- [ ] `AuditLog`'s XML doc corrected — it currently documents the defect as the design
- [ ] The PR body states whether this lands before any deployment writes real history
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
