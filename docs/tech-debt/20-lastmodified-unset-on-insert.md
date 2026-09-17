# 20 — `LastModifiedOn` is left at its sentinel on every insert

Status: **open** · Kind: correctness · Impact: silent-wrong · Area: Audit
Source: found 2026-09-17 while repointing the PRD · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

`AuditSaveChangesInterceptor.Apply()` assigns the audit columns **per entry state**, not wholesale
([AuditSaveChangesInterceptor.cs:58-72](../../src/api/TrailBlaze.Repository/AuditSaveChangesInterceptor.cs#L58-L72)):

| Entry state | Fields assigned |
|---|---|
| `Added` | `CreatedOn`, `CreatedBy` (only where null), `IsDeleted = false` |
| `Modified` | `LastModifiedOn`, `LastModifiedBy` |

`LastModifiedOn` and `LastModifiedBy` are therefore **not assigned on insert** — and nothing else
fills them. `EntityBase` declares them with no initialiser, and `DatabaseRepository.CreateAsync<T>(T item)`
is `AddAsync` + `SaveChangesAsync` and sets nothing
([DatabaseRepository.cs:46-52](../../src/api/TrailBlaze.Repository/DatabaseRepository.cs#L46-L52)).

So a row that has never been updated is stored with:

- `LastModifiedOn = default(DateTimeOffset)` — `0001-01-01T00:00:00+00:00`
- `LastModifiedBy = null`

`LastModifiedOn` is `nullable: false` in the migration
([InitialCreate.cs:48](../../src/api/TrailBlaze.Repository/Migrations/20260915144435_InitialCreate.cs#L48)),
with no `defaultValue`, so the sentinel is not the caller's choice — the column simply cannot express
"never modified", and this is what it says instead.

## Why it matters

**A freshly created row reports that it was last modified in the year 1.** Anything that renders the
field — a detail view, an export, a sort by "recently changed" — shows or ranks a date that is wrong
rather than absent. Nothing throws, no test fails, and the value is plausible enough to survive
review: it *looks* like a date.

**The tests encode the gap instead of catching it.** `AuditTests` asserts `CreatedOn != default` and
`CreatedBy` on insert ([AuditTests.cs:72-73](../../src/api/TrailBlaze.Repository.Test/AuditTests.cs#L72-L73)),
and asserts `LastModifiedOn != default` only on **update**
([:193-194](../../src/api/TrailBlaze.Repository.Test/AuditTests.cs#L193-L194)). The insert assertions
stop exactly where the unset field begins — so the omission is mirrored in the test file rather than
noticed by it. This is item [12](12-feature-02-tests-deferred.md)'s argument in miniature: a guard
that is absent looks identical to a guard that passes.

**The PRD documents the opposite.** [PRD.md:163](../PRD.md#L163) states `LastModifiedOn` is "set at
insert and on every update". It is not set at insert. That sentence was re-checked and found false
while repointing the PRD at this register — the same shape as §12.1, a written claim about these
columns that the code does not honour, surviving because nothing checks.

**It is distinct from item [01](01-audit-columns-have-two-writers.md), and does not close with it.**
01's repair deletes the *dead* writes on the delete path; it neither adds nor removes anything on the
insert path. Same columns, different code, different fix.

## Evidence

Checked against the code and the suite 2026-09-17.

- The interceptor's switch has `Added` and `Modified` cases; only `Modified` touches
  `LastModifiedOn`/`LastModifiedBy`.
- `EntityBase` declares `public DateTimeOffset LastModifiedOn { get; set; }` with no initialiser.
- `CreateAsync<T>(T item)` and `CreateAsync<T>(List<T>)` both set nothing beyond the add.
- `InitialCreate.cs:48` — `nullable: false`, no `defaultValue`.
- `dotnet test` 2026-09-17: **42 runnable, 41 passing, 1 skipped.** Nothing asserts the insert case.

## The decision this item is really asking for

Before fixing, choose between two readings. They are not equivalent, so this is a design choice
rather than a bug fix:

1. **Set `LastModifiedOn` on insert, equal to `CreatedOn`.** The column always holds a real instant,
   and "last modified" of a fresh row means "created". `LastModifiedBy` follows `CreatedBy`. This is
   the conventional reading of the pair, and `LastModifiedOn == CreatedOn` still lets a reader ask
   whether a row was ever touched.
2. **Make `LastModifiedOn` nullable, so a never-modified row says so.** The sentinel disappears
   rather than being papered over, and "untouched" becomes expressible — at the cost of a schema
   change on a live table and a null branch in every reader.

**Recommendation: (1).** The distinction (2) buys is one no current reader needs, and a nullable
column on a live table is the larger and harder-to-reverse cost. Whichever is chosen, the PRD line
and the audit tests are corrected with it, and the choice is recorded in the PR body — not left for
the diff to imply.

## Testability

**testable**, and red-first is immediate: assert `LastModifiedOn != default` and
`LastModifiedBy == the caller` on an **inserted** row and watch it fail.
`TestSupport/AuditHarness.cs` exists and `AuditTests` already asserts the insert case for `CreatedOn`,
so this is one more assertion in an existing test — not a new tier, and not dependent on item 12.

## Repair plan

1. **RED** — add the insert assertion beside the existing `CreatedOn` one in `AuditTests`. It fails
   today.
2. **Decide (1) or (2)** as above, and say which in the PR.
3. **GREEN** — under (1), assign `LastModifiedOn = now` and `LastModifiedBy = actor` in the `Added`
   branch. Under (2), make the pair nullable and add the migration.
4. **Correct `PRD.md:163`** to whatever the decision makes true.
5. **Address rows already carrying the sentinel.** Under (2) the migration must decide what existing
   rows become; a backfill to `CreatedOn` is the only value that is not invented. Under (1) there is
   nothing to backfill for new rows, but existing rows keep the sentinel until written — say whether
   that is accepted or backfilled.

## Out of scope / related

- **Item [01](01-audit-columns-have-two-writers.md)** — same columns, different defect (a dead writer
  on the delete path). Neither discharges the other.
- **Item [06](06-timestamp-types-inconsistent.md)** is a facet of 01 and does not touch this.
- **`IsDeleted = false` is assigned in the same `Added` branch** — a third, unrelated decision in
  those three lines. Noted so whoever fixes this does not assume the branch is only about timestamps.
- **The `Modified` branch is not in question.** It is correct, and item 01 is about a *second* writer
  competing with it.

## Close checklist

- [ ] The insert behaviour is decided (CreatedOn-equivalent, or nullable-and-honest) and recorded
- [ ] A test asserts the insert case, and was seen to fail before the fix
- [ ] `PRD.md:163` matches the decision
- [ ] Rows already carrying the sentinel are backfilled or the omission is explicitly accepted
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
