# 23 — Feature specs assert foreign keys and a cascade the model does not have

Status: **open** · Kind: docs · Impact: friction · Area: Model
Source: found 2026-09-18 (during the container-backed test change) · Discharges via: features 04/06 (in part) · Opened: 2026-09-18 · Last verified: 2026-09-18

## What the debt is

Four statements across two feature specs describe relationships the EF model does not declare:

| Where | Claim |
|---|---|
| [04-activity-crud.md:32](../features/04-activity-crud.md#L32) | `CreatedByUserId` **FK** → `users.Id` |
| [04-activity-crud.md:80-81](../features/04-activity-crud.md#L80-L81) | the **FK** to `users.Id` is asserted from the EF model; "the cascade to media rows is a mapping" |
| [06-media-upload.md:28](../features/06-media-upload.md#L28) | 04's **FK gives the cascade delete** |
| [06-media-upload.md:60](../features/06-media-upload.md#L60) | **acceptance criterion:** `DELETE /api/activities/{id}` cascades to media rows "(FK cascade)" |

**The model declares no foreign keys at all.**

```
grep -rn "HasForeignKey|HasOne|HasMany|OnDelete" src/api/TrailBlaze.Repository
→ no matches
```

Every entity table stands alone. `AuditLog` is deliberately not an `EntityBase`, and nothing else
declares a relationship or a navigation property.

## Why it matters

**One of these is an acceptance criterion, and it cannot be met by the mechanism it names.**
[06:60](../features/06-media-upload.md#L60) requires that deleting an activity removes that
activity's media rows "regardless of uploader". Written as a cascade, that is satisfied by the
schema and nothing in the application. With no FK, it is **work the service layer must do**, and
because it is also an access-control rule — an owner must be able to delete an activity holding
media someone else contributed — it is exactly the kind of logic that must be tested rather than
inherited from the database.

So the spec currently points an implementer at a schema guarantee that does not exist, and the
behaviour would be silently absent: no error, no failing test, just orphaned rows and unreclaimed
blobs. That is why this is filed rather than left as a wording nit.

**It is `friction` and not `silent-wrong` because nothing is wrong today.** Features 04 and 06 are
unbuilt, so no code is missing a cascade. The cost is paid by whoever implements them, which is soon
enough to matter and not yet a defect.

**A related claim was retired in the same pass.** `testing-and-tdd.md` listed "cascade deletes" as a
repository-tier concern; that row is deleted, and the reason is recorded there. The two documents
disagreeing about the same absent feature is itself the reason this is one item rather than two.

## Evidence

Checked 2026-09-18.

- The grep above returns nothing, so no relationship is configured anywhere.
- `UserModelTests` and `PersistenceModelTests` assert columns, types and lengths — never a
  relationship, which is consistent with there being none.
- The container tier proves the same thing from the other side: `MigrationNarrowingTests` inserts
  into `Users` directly with no companion row, and `DuplicateKeyTests` seeds a lone `User`. Neither
  needs a parent.

## Testability

**doc-assertion.** The invariant is "no specification in `docs/features/` claims a foreign key or a
cascade that the model does not declare". A test can enumerate the spec files, assert that any
occurrence of "FK"/"cascade"/"foreign key" is accompanied by the schema actually declaring one, and
go red when a spec is reverted to the current text. That is a real failure mode, and it is the shape
STANDARD §10 sanctions for a durable property of a file.

The behavioural half is not this item's: whether deleting an activity removes its media is feature
06's acceptance criterion, and it becomes testable when the code exists.

## Repair plan

1. **Decide the schema question first, because the specs should follow the answer, not lead it.**
   - *(a)* **Keep the model relationship-free** (today's state) → 06:60's cascade becomes explicit
     service work in a transaction, with its own test; correct all four statements to say "no FK, the
     service removes them".
   - *(b)* **Declare the FKs** → then the specs are nearly right and the change is a migration plus
     the `OnDelete` behaviour decision per relationship. Note this pulls `AuditLog` into the question,
     since it is deliberately outside `EntityBase`.
2. Correct whichever statements the answer does not satisfy, in the same commit.
3. Add the `doc-assertion` test so the claim cannot drift back.
4. Do this with features 04/06 rather than ahead of them — they own the tables.

**Recommendation: (a), and leave the model alone.** The repo has shipped two features without a
relationship and the schema's independence is consistent with `AuditLog`'s design; a cascade would
move an access-control decision into DDL where it cannot be unit-tested. The specs are the thing
that is wrong.

## Out of scope / related

- **This is not the same as item [21](21-container-access-levels-in-prose.md).** That one is a
  declared-but-unset access level; this one is an undeclared-and-assumed relationship. Both are
  "prose is doing a schema's job", which is a pattern worth noticing but not one item.
- **`IDbRepository.DeleteAsync` is a soft delete** ([item 02](02-deleteasync-n-round-trips.md)), so
  even with a cascade the rows would be marked, not removed. Any repair must say which it means.

## Close checklist

- [ ] No spec claims a foreign key or cascade the model does not declare
- [ ] Feature 06's delete criterion states the mechanism that actually removes the media rows
- [ ] The mechanism is tested, including the cross-uploader case the criterion names
- [ ] The `doc-assertion` test exists and goes red when a spec is reverted
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
