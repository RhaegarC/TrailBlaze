# 23 — Documentation asserts foreign keys and a cascade the model does not have

Status: **open** · Kind: docs · Impact: friction · Area: Model
Source: found 2026-09-18 (during the container-backed test change) · Discharges via: features 04/06 (in part) · Opened: 2026-09-18 · Last verified: 2026-09-19

## What the debt is

Statements across the docs describe relationships the EF model does not declare. **The four in
feature specs were corrected on 2026-09-19**, which is what makes the rest urgent rather than
merely wrong: the PRD is now the only document an implementer reads that draws an FK which does
not exist.

| Where | Claim | State |
|---|---|---|
| [PRD — data model](../PRD.md#data-model), ER diagram | `CreatedByUserId`, `ActivityId`, `UploadedByUserId` drawn as `FK` | `CreatedByUserId` fixed 2026-09-19 by feature 04; the two `media` ones open |
| [PRD — data model](../PRD.md#data-model), column table | those three rows read **FK →** another table's id | `activities.CreatedByUserId` fixed 2026-09-19 by feature 04; the two `media` rows open |
| [PRD — API surface](../PRD.md#api-surface) | `DELETE /api/activity/{id}` — "Delete (cascades media)" | fixed 2026-09-20 — and the route now says something else entirely: see the update below |
| [04 — acceptance criteria](../features/archive/04-activity-crud.md#acceptance-criteria) | `CreatedByUserId` **FK** → `users.Id` | fixed 2026-09-19 |
| [04 — tests](../features/archive/04-activity-crud.md#tests-tdd) | the **FK** to `users.Id` is asserted from the EF model; "the cascade to media rows is a mapping" | fixed 2026-09-19 |
| [06 — dependencies](../features/06-media-upload.md#dependencies) | 04's **FK gives the cascade delete** | fixed 2026-09-19 |
| [06 — acceptance criteria](../features/06-media-upload.md#acceptance-criteria) | `DELETE /api/activity/{id}` cascades to media rows "(FK cascade)" | fixed 2026-09-19 |

**The model declares no foreign keys at all.**

```
grep -rn "HasForeignKey|HasOne|HasMany|OnDelete" src/api/TrailBlaze.Repository
→ no matches
```

Every entity table stands alone. `AuditLog` is deliberately not an `EntityBase`, and nothing else
declares a relationship or a navigation property.

## Update 2026-09-20 — the behaviour this item assumed was decided the other way

**The premise in "Why it matters" is no longer the product's.** That section argued the absence of
an FK turned the cascade into service work that had to be written and tested. Feature 06's review
removed the cascade instead: `IDbRepository.DeleteAsync` is a soft delete (item
[02](02-deleteasync-n-round-trips.md) says so, and this item's own "Out of scope" noted it), so
deleting an activity is recoverable — and media removed alongside it could not be recovered with it.
`ActivityService.DeleteAsync` now removes the activity alone, and a restore brings the media back.
The PRD's route note and 06's criterion and dependency bullet were corrected to say that in the same
change.

**What that leaves open here is only the two `media` `FK →` labels** in the PRD's ER diagram and
column table. The cascade half of this item is discharged — not by writing the mechanism it
predicted, but by the product declining it — and the close checklist below is annotated rather than
ticked, so the reasoning is visible to whoever closes this.

## Why it matters

**One of these is an acceptance criterion, and it cannot be met by the mechanism it named.**
[06's delete criterion](../features/06-media-upload.md#acceptance-criteria) requires that deleting
an activity removes that activity's media rows "regardless of uploader". Written as a cascade, that
is satisfied by the schema and nothing in the application. With no FK, it is **work the service
layer must do**, and because it is also an access-control rule — an owner must be able to delete an
activity holding media someone else contributed — it is exactly the kind of logic that must be
tested rather than inherited from the database. The wording was corrected on 2026-09-19; the
mechanism is still unbuilt, because 06 is.

Left as it stood, an implementer would have followed a schema guarantee that does not exist and the
behaviour would have been silently absent: no error, no failing test, just orphaned rows and
unreclaimed blobs. That is why this is filed rather than left as a wording nit.

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

**doc-assertion, and it is built.** The invariant is "no feature spec claims a foreign key or a
cascade that the model does not declare". `scripts/doc-assert.py` carries it as *No spec asserts a
relationship the model does not declare*: it flags `FK →`, `(FK cascade)` and an `FK` within a
sentence of "cascade" in any non-archived spec, and it lifts itself the moment the model declares a
relationship. It was verified to go red by reintroducing `CreatedByUserId` FK → `users.Id` into 04.
The PRD is deliberately outside it: its data model is product content rather than a build
instruction, so the table above is what holds those rows to account.

The behavioural half is not this item's: whether deleting an activity removes its media is feature
06's acceptance criterion, and it becomes testable when the code exists.

## Repair plan

1. **Decide the schema question first, because the specs should follow the answer, not lead it.**
   - *(a)* **Keep the model relationship-free** (today's state) → 06's cascade becomes explicit
     service work in a transaction, with its own test; correct the PRD's three `FK →` labels, its ER
     diagram and its "cascades media" route note to say "no FK, the service removes them".
   - *(b)* **Declare the FKs** → then the specs are nearly right and the change is a migration plus
     the `OnDelete` behaviour decision per relationship. Note this pulls `AuditLog` into the question,
     since it is deliberately outside `EntityBase`.
2. Correct whichever statements the answer does not satisfy, in the same commit.
3. The `doc-assertion` test exists now (`scripts/doc-assert.py`); extend it to the PRD when the PRD
   is corrected, if the PRD is to be watched at all.
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

- [x] No spec claims a foreign key or cascade the model does not declare — 2026-09-19
- [ ] The PRD's **two** remaining `FK →` labels in the `media` rows and the ER diagram are corrected *(the third, on the route note, is done — it now states the soft delete, 2026-09-20)*
- [x] Feature 06's delete criterion states what the delete actually does — 2026-09-20 (it leaves the media standing, and the criterion says so)
- [x] The behaviour is tested, whichever way it was decided — 2026-09-20 (unit: the delete reaches no media; container: an activity's deletion leaves its media rows live)
- [x] The `doc-assertion` test exists and goes red when a spec is reverted — 2026-09-19
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
