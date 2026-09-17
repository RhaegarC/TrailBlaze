# 02 — `DeleteAsync` does N round trips and is not transactional

Status: **open** · Kind: correctness · Impact: silent-wrong · Area: Persistence
Source: STANDARD §12.2 · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

`DatabaseRepository.DeleteAsync` takes a list of ids and issues one `FindAsync` per id, then a
single `SaveChangesAsync` with no transaction around the batch —
[DatabaseRepository.cs:73-89](../../src/api/TrailBlaze.Repository/DatabaseRepository.cs#L73-L89):

```csharp
foreach (var id in ids)
{
    T? item = await Context.FindAsync<T>(id);
    if (item != null)
    {
        item.IsDeleted = true;
        // ...
        Context.Update(item);
    }
}

int count = await Context.SaveChangesAsync();
```

Two problems in nine lines:

1. **N+1 round trips.** Deleting 50 items is 51 queries. A batch method that costs the same as
   calling the single-item path in a loop is not a batch method.
2. **Partial application.** An exception after the third `FindAsync` leaves two items deleted in the
   change tracker and zero persisted — or, worse, a failure at `SaveChangesAsync` after some earlier
   `Update` calls in a different code path has no defined rollback, because nothing scoped the batch
   in a transaction.

## Why it matters

**It is invisible until it is expensive, and then it is very hard to see why.** Nothing fails at
small scale: three items delete in three round trips and the caller never notices. By the time the
volume makes it hurt, the shape of the loop is buried under a method that looks like it does the
batch work properly — the signature `DeleteAsync<T>(List<string> ids)` is a promise the body does not
keep.

The partial-apply half is the more serious one and the harder to test. A delete that half-succeeds
leaves soft-deleted rows the caller believes were not deleted, and because soft delete writes
`IsDeleted` rather than removing rows, there is no constraint violation or missing-row error to
surface it. The caller's retry then produces a different count than the first attempt, and the count
is the only feedback the method gives.

**No caller exists yet** — there is no `Activity` entity, so nothing in production reaches this
path. That is why this is debt and not a bug: it is a latently wrong implementation waiting for
feature 04 to depend on it. It becomes urgent the moment an activity delete endpoint is written
against it, which is the argument for fixing it before 04 rather than after.

## Evidence

Checked against the code 2026-09-17.

- The loop and the untransacted `SaveChangesAsync` are at
  [DatabaseRepository.cs:75-88](../../src/api/TrailBlaze.Repository/DatabaseRepository.cs#L75-L88).
- STANDARD §4 already names this defect as the worked example of a rule it states: "never start a
  task inside a loop that touches the `DbContext`. See section 12 for a batch-write bug of exactly
  this shape found in the repository"
  ([STANDARD.md:275-277](../../src/api/STANDARD.md#L275-L277)). The standard warns about this code
  while the code still contains it — which is the divergence, stated in the standard rather than
  fixed in the repository.
- `IDbRepository.DeleteAsync<T>` has no production caller (grepped, 2026-09-17).

## Testability

**testable** at the repository tier, and the no-database harness is what makes it possible: SQL is
inspected without a connection, so the round-trip count can be asserted rather than benchmarked.

- **Round trips**: assert the generated SQL / the number of executed commands is one statement for a
  multi-id delete, not one per id. The harness's `ToQueryString()` pattern already exists in
  `TrailBlaze.Repository.Test`.
- **Transactional scope**: assert that a failure mid-batch leaves nothing deleted. This needs the
  harness to inject a failure partway, which is the part to design first — if it cannot be made to
  fail, say so in the PR rather than writing an assertion that cannot go red.

## Repair plan

1. **Decide the shape first**, because the two halves have different fixes:
   - Replace the per-id `FindAsync` loop with a single set-based read (`Where(x => ids.Contains(x.Id))`)
     or a single `ExecuteUpdate`, depending on whether the soft-delete audit path must still run per
     entity. The interceptor needs the entries tracked, so this is a real choice, not a style one —
     `ExecuteUpdate` would bypass the audit log entirely and is probably wrong here.
   - Wrap the batch in an explicit transaction (`Context.Database.BeginTransactionAsync`) so a
     partial apply is impossible.
2. RED first: the round-trip assertion, then the partial-failure assertion.
3. GREEN: the set-based read and the transaction.
4. **Consider whether the batch method should exist.** Item [16](16-unreferenced-scaffolding.md) notes
   it has no caller and its siblings are equally untested. Deleting the overload is a legitimate
   resolution — but if feature 04 will want it, fixing it now is cheaper than rediscovering it.

## Out of scope / related

- **Item [01](01-audit-columns-have-two-writers.md)** touched the same method and is now landed: it
  deleted the dead `"sys"` writes that sat at lines 81–82, so `DeleteAsync` is two lines shorter and
  the line anchors elsewhere in this file predate that. Fixing 02 will edit the same method, and its
  redesign deserves its own RED-first evidence — the caveat being that 01 was a deletion and changed
  no behaviour, which 02 does.
- **Item [16](16-unreferenced-scaffolding.md)** covers the broader question of committed-but-unused
  repository surface; this item is the case where the unused code is also wrong.
- **Soft delete semantics** (item [05](05-soft-delete-recorded-as-modified.md)) are adjacent but
  independent — 05 is about what the audit log *records*, this is about how the write is *performed*.

## Close checklist

- [ ] A test asserting one statement for a multi-id delete, confirmed to fail before the change
- [ ] A test asserting no partial apply on mid-batch failure, or a stated reason it cannot be written
- [ ] The transaction decision recorded in the PR body (why tracked entities rather than `ExecuteUpdate`)
- [ ] STANDARD §4's reference updated from "see section 12" to this item
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
