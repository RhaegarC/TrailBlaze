# 01 — Audit columns had two writers, and one of them was dead

Status: **Archived** — resolved by deleting the dead writer, merged to `develop` in PR #8 · [00-debt-log.md](../00-debt-log.md)
Source: STANDARD §12.1 · Opened: 2026-09-17 · Archived: 2026-09-18

## What it was

Two code paths wrote the same audit fields on a soft delete, and the one that ran second overwrote
the first.

`DatabaseRepository.DeleteAsync` wrote them by hand:

```csharp
item.IsDeleted = true;
item.LastModifiedOn = DateTime.UtcNow;
item.LastModifiedBy = "sys";
Context.Update(item);
```

`Context.Update(item)` moves the entry to `Modified`, so `AuditSaveChangesInterceptor.Apply()` —
which runs on every save — hit the `EntityState.Modified` case and overwrote both fields:

```csharp
case EntityState.Modified:
    entry.Entity.LastModifiedOn = now;      // DateTimeOffset.UtcNow
    entry.Entity.LastModifiedBy = actor;    // ResolveActor() — the real caller
    break;
```

So `LastModifiedBy = "sys"` never reached the database; the stored actor was the caller.

**Nothing a caller could see was wrong**, which is what made this debt rather than a bug: the correct
writer won. Had the order been reversed it would have been a privacy-adjacent defect — a real user's
action attributed to the system — and would have belonged in `docs/bugs/` as Critical. The boundary
is worth keeping as the register's clearest statement of where debt stops and a bug begins.

`DeleteAsync` had no caller in production code — no `Activity` entity exists yet — so the dead branch
was unreachable as well as inert. That is why nothing had ever noticed.

## How it resolved

The two assignments were deleted in PR #8 on 2026-09-17. `DeleteAsync` now sets `IsDeleted` and calls
`Context.Update(item)`, which is the whole of what a soft delete does, and a comment sits where the
assignments were saying why the audit columns are not stamped there — a future reader re-adding them
is the exact cost this item described.

Corrected in the same pull request:

- **STANDARD §3**, which asserted in the present tense that the dead writer was still there.
- **STANDARD §12.1**, which carried a second false claim this item had not named: that the
  interceptor "stamps all four on every save". It stamps per entry state and leaves `LastModifiedOn`
  at its sentinel on insert — which is item [20](../20-lastmodified-unset-on-insert.md).
- **Items [02](../02-deleteasync-n-round-trips.md) and [05](../05-soft-delete-recorded-as-modified.md)**,
  whose line anchors pointed into the deleted lines.

### Testability: `verification-only`

The deletion is behaviour-neutral by construction — the interceptor assigns both fields
unconditionally on the same save — so no behavioural test can distinguish before from after, and one
written for it would fail STANDARD §10's *"a test that cannot fail is not a test"*.

What was verified instead:

- `dotnet test` before and after: **41 passed, 1 skipped, 42 total, both times, no test moving.** A
  regression would have shown here.
- `grep -rn "[^f]DateTime\.UtcNow" src/api/` → **none found.** That was also item
  [06](06-timestamp-types-inconsistent.md)'s requested evidence.

**This is a verified close, not a tested one.** It is weaker, and the register's rule for the value is
what keeps the two from reading alike.

### The guard was weaker than this item claimed

As filed, the item named the existing `AuditTests` as its guard — "if either goes red, the writes were
not dead". They were not a guard on this change: both prove the interceptor's `Added`/`Modified`
paths, and **neither calls `DeleteAsync`**, which had no test at all. They were the reasoning behind
the deletion, not an assertion on it. The item was corrected rather than left implying a safeguard it
did not have, and its close-checklist box for "the stored actor is still the caller" was left
unchecked deliberately.

`DeleteAsync` still has no test of any kind. Feature [04](../../features/04-activity-crud.md) is
its first caller, and covering it belongs there.

## Lesson

**A claim in a trusted document outlives the code it describes, and this item is the register's
founding example.** STANDARD §12.1 asserted for months that the audit columns were unmaintained,
after the interceptor had started maintaining them — a document whose whole purpose is to be
trustworthy about exactly that was wrong about it, and nothing noticed because nothing checked. The
same false claim turned up in [PRD.md](../../PRD.md) too.

The second lesson is narrower and was learned during the fix: **an item's own guard claim needs
re-verifying.** This one named two tests as its safety net and neither touched the method being
changed; a fixer who read that section and trusted it would have had no net at all.
