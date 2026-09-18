# 24 — Every write method returns the entry count, which is not the rows the caller named

Status: **open** · Kind: correctness · Impact: silent-wrong · Area: Persistence
Source: found 2026-09-18 (during the container-backed test change) · Discharges via: — (orphaned) · Opened: 2026-09-18 · Last verified: 2026-09-18

## What the debt is

`IDbRepository`'s write methods return `int`, and every one of them returns
`SaveChangesAsync()`'s value verbatim:

```csharp
int count = await Context.SaveChangesAsync();
return count;
```

That number is **the count of state entries EF wrote**, not the number of rows the caller asked
about. `AuditSaveChangesInterceptor` adds an `AuditLog` entry to the same `SaveChanges` call, so the
caller's row is counted alongside its audit row:

| Call | Returns | Rows the caller named |
|---|---|---|
| `CreateAsync(user)` | 2 | 1 |
| `UpdateAsync(user)` | 2 | 1 |
| `DeleteAsync([id])` | 2 | 1 |

Measured, not inferred: deleting one user through the container tier returns **2**.

## Why it matters

**The value is a plausible wrong answer rather than an obvious one.** `count == 0` still correctly
detects "nothing to do", which is what makes this survivable — but any caller that compares the
result to the number of ids it passed (`count == ids.Count`) fails, and one that reports the number
tells a user "2 items deleted" for a single row. The signature invites that second reading:
`DeleteAsync<T>(List<string> ids)` takes a list and returns an `int`, which reads as "how many of
these were deleted".

**Nothing is wrong today, which is why this is debt.** No production caller uses the return value:
`UserService` awaits `CreateAsync`/`UpdateAsync` and discards the result, and the `List<string>`
overload has no caller at all yet ([item 05](05-soft-delete-recorded-as-modified.md) records the same
absence for the soft-delete path). So the register's boundary holds — nothing a caller saw is wrong —
and this is a wrong value waiting to be consumed rather than a defect in flight.

**It is also a contract question, not a bug in a line.** The fix is to decide what the number means,
and the options differ in kind: rows the caller named, or entries persisted. Both are defensible; a
method whose name says `DeleteAsync` and whose value counts audit rows is not.

## Evidence

Checked 2026-09-18.

- [`DatabaseRepository.cs:46-109`](../../src/api/TrailBlaze.Repository/DatabaseRepository.cs#L46-L109)
  — all five write methods (`CreateAsync` ×2, `DeleteAsync`, `UpdateAsync` ×2) return
  `SaveChangesAsync()`'s value with no adjustment.
- The container tier measures it: a delete of one `User` returns **2**, the second entry being the
  interceptor's `AuditLog` row for the same save.
- `grep -rn "CreateAsync|UpdateAsync" src/api --include=*.cs` outside the repository and the tier
  finds only `UserService` call sites, all of which discard the value.

## Testability

**testable**, and the container tier is already the right place — this is a claim about what a save
writes, which the change tracker cannot answer and a real engine can.

The assertion is a pair rather than a single number, because the interesting failure is the audit row
being counted: write one entity with the interceptor active and assert both the returned value and
the row count, then assert the same for a soft delete. The current behaviour is measurable, so this
is an ordinary RED → GREEN. Record the measured numbers in the test, as the tier's other
engine-specific constants are.

## Repair plan

1. **Decide what the int means, and write it on the interface.** "Rows the caller named" and "entries
   persisted" are both usable; only one of them can be what a member named `DeleteAsync` returns.
2. Implement it in one place — every write method currently repeats the same two lines, so the
   translation belongs in a private helper rather than five copies.
3. If the answer is "rows the caller named", the audit entry must be excluded, which means asking the
   interceptor what it added rather than counting the change tracker afterwards.
4. RED first, against the container tier, asserting the number and the rows.
5. Correct the XML docs on `IDbRepository`'s write members, which currently say nothing about what
   the int counts. That omission is what let this go unnoticed.

**Recommendation: rows the caller named,** computed by subtracting the interceptor's own entries. It
is what the signatures read as, it is what a caller can act on, and the alternative makes a public
API's return value depend on an interceptor's implementation detail.

## Out of scope / related

- **[Item 02](02-deleteasync-n-round-trips.md)** owns the same method's round-trip count and its
  missing transaction. That item and this one touch the same lines and should land together or in a
  stated order, not independently.
- **The interceptor itself is correct** — writing one audit row per change is its job. This item is
  about a caller-facing number that silently includes it.

## Close checklist

- [ ] `IDbRepository`'s write members document what their return value counts
- [ ] The value matches that documentation, asserted against a real engine
- [ ] The audit entry is excluded or explained, and a test names which
- [ ] No write method repeats the translation by hand
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
