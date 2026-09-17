# 01 — Audit columns have two writers, and one of them is dead

Status: **open** · Kind: correctness · Impact: silent-wrong · Area: Audit
Source: STANDARD §12.1 · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

Two code paths write the same audit fields on a soft delete, and the one that runs second
overwrites the first.

`DatabaseRepository.DeleteAsync` writes them by hand —
[DatabaseRepository.cs:80-83](../../src/api/TrailBlaze.Repository/DatabaseRepository.cs#L80-L83):

```csharp
item.IsDeleted = true;
item.LastModifiedOn = DateTime.UtcNow;
item.LastModifiedBy = "sys";
Context.Update(item);
```

`Context.Update(item)` moves the entry to `Modified`, so `AuditSaveChangesInterceptor.Apply()` —
which runs on every save — hits the `EntityState.Modified` case and overwrites both fields
([AuditSaveChangesInterceptor.cs:67-70](../../src/api/TrailBlaze.Repository/AuditSaveChangesInterceptor.cs#L67-L70)):

```csharp
case EntityState.Modified:
    entry.Entity.LastModifiedOn = now;      // DateTimeOffset.UtcNow
    entry.Entity.LastModifiedBy = actor;    // ResolveActor() — the real caller
    break;
```

So `LastModifiedBy = "sys"` never reaches the database. The stored actor is the caller.

## Why it matters

**The recorded claim was false.** STANDARD §12.1 said the audit columns "are not maintained" and
`CreatedOn` is `default(DateTime)`. Neither is true, and had been untrue since the interceptor
landed. This item is the register's founding example: a divergence that stayed in a document
everyone trusted, describing a bug that had already been fixed, while the real defect — a
conflicting second writer — went unremarked.

Left alone, the cost is that the next reader repeats the mistake. Anyone fixing "unmaintained audit
columns" writes code for a problem that does not exist, and the `"sys"` literal reads as a
deliberate attribution policy rather than a line that never executes. The `"sys"` hardcode is also
the kind of thing that gets copied — it is one file away from being the pattern for the next
repository method.

**Nothing a caller can see is wrong today**, which is what makes this debt rather than a bug: the
correct writer wins. If the order were reversed, this would be a privacy-adjacent defect — a real
user's action attributed to the system — and would belong in `docs/bugs/` as Critical.

## Evidence

Checked against the code 2026-09-17.

- The interceptor stamps all four columns: `CreatedOn`/`CreatedBy` on `Added`
  ([AuditSaveChangesInterceptor.cs:62-66](../../src/api/TrailBlaze.Repository/AuditSaveChangesInterceptor.cs#L62-L66)),
  `LastModifiedOn`/`LastModifiedBy` on `Modified` ([…:67-70](../../src/api/TrailBlaze.Repository/AuditSaveChangesInterceptor.cs#L67-L70)).
- Existing tests already prove it works: `An_insert_stamps_the_audit_columns`
  (`AuditTests.cs:64-75`) and `A_modified_entity_is_recorded_as_modified_and_stamped`
  (`AuditTests.cs:180-195`). Both pass today.
- `DeleteAsync` has no caller in production code — no `Activity` entity exists yet — so the dead
  branch is currently unreachable, which is why nothing has ever noticed.

## Testability

**testable**, in two parts, because the repair is a deletion:

1. **The deletion is behaviour-neutral**, so it needs no new test and a new test would be a lie —
   STANDARD §10's "a test that cannot fail is not a test" applies. The guard is that the existing
   `AuditTests` stay green: if either goes red, the writes were not dead and this item's premise is
   wrong. That is the check to run first, before editing anything.
2. **The doc claim is assertable** (`doc-assertion`): a test that fails if STANDARD §3 or §12 states
   the audit columns are unmaintained. That is the artifact that would have caught this item a month
   earlier, and item [19](19-doc-indexes-drifted.md) is where the general mechanism belongs.

## Repair plan

1. Run `dotnet test` and confirm the two `AuditTests` above are green before touching anything.
2. Delete lines 81–82 of `DatabaseRepository.cs` — the `LastModifiedOn` and `LastModifiedBy` writes.
   Keep `IsDeleted = true` and `Context.Update(item)`: the first is the actual soft delete, and the
   second is what puts the entry in front of the interceptor in the first place.
3. Confirm `dotnet test` is still green. No test should move.
4. Correct STANDARD §3 and §12.1 (see item [19](19-doc-indexes-drifted.md)) so the false claim dies
   with the code.
5. Item [06](06-timestamp-types-inconsistent.md) closes with this one — its `DateTime`/`DateTimeOffset`
   mismatch exists only inside the writes deleted here.

## Out of scope / related

- **Why `DeleteAsync` writes them at all** is worth knowing before deleting: the interceptor arrived
  later than the repository, so these two lines were correct when written. This is not carelessness
  to be reviewed, it is a superseded path to be removed.
- **The `Modified` state is a separate defect.** `Context.Update(item)` on a soft delete is what
  makes the audit log record the action as `"Modified"` — item
  [05](05-soft-delete-recorded-as-modified.md). Do not fix 05 here; deleting these two lines does not
  change the entry's state, so 05 survives this repair untouched and should.
- **`CreatedBy ??= actor`** in the interceptor means a caller-set `CreatedBy` wins. Nothing currently
  sets it, but it is the one place this design could be bypassed. Not this item.

## Close checklist

- [ ] `AuditTests` green before the change (premise check) and after (no regression)
- [ ] Lines 81–82 of `DatabaseRepository.cs` deleted
- [ ] `LastModifiedBy` in the database is still the caller, not `"sys"` — confirmed via the existing
      audit assertion, which is the evidence the deletion was safe
- [ ] STANDARD §3 and §12.1 corrected in the same PR
- [ ] Item [06](06-timestamp-types-inconsistent.md) moved to resolved
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
