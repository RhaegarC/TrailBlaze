# 16 — Members with no production caller, indistinguishable from leftovers

Status: **open** · Kind: hygiene · Impact: friction · Area: Persistence
Source: found 2026-09-17 (not a §12 item) · Discharges via: **features 06, 07, 08** (in part) · Opened: 2026-09-17 · Last verified: 2026-09-18

## What the debt is

`IStorageRepository` is fully implemented, fully documented, and partly **unused**. Four members
have no production caller — their only consumers are tests:

| Member | Production callers | Built for |
|---|---|---|
| `CreateReadUrlAsync` | **none** | feature 07 — SAS delivery of private media |
| `MoveAsync` | **none** | feature 08 — visibility change between public and private containers |
| `Constant.StorageContainer.Covers` | **none** | feature 08 — cover images |
| `Constant.StorageContainer.Media` | **none** | feature 06 — activity media |

The rest of the interface is genuinely live: `UploadAsync`
([UserService.cs:169](../../src/api/TrailBlaze.Service/UserService.cs#L169)), `DeleteAsync`
([:181](../../src/api/TrailBlaze.Service/UserService.cs#L181),
[:213](../../src/api/TrailBlaze.Service/UserService.cs#L213)), `CreatePublicUrl`
([:232](../../src/api/TrailBlaze.Service/UserService.cs#L232)) and
`Constant.StorageContainer.Avatars` are all reached from the avatar slice.

## Why it matters

**This is not "code written too early" — the seam-first design is deliberate and its interface says
so. The debt is that nothing distinguishes it from a leftover.** A reader who runs `impact` on
`MoveAsync` gets `risk: UNKNOWN` with an empty caller set — precisely the verdict CLAUDE.md says is
*not* an all-clear, because an empty caller set can mean "unused" or "not resolvable by the index".
The documentation effort went into explaining *why each member is shaped this way*; none went into
saying *"this has no caller yet, on purpose, until feature 08."*

**That ambiguity is not hypothetical here.** This repository has already done one cleanup pass over
this exact layer: `AzureBlobStorageService` moved from `TrailBlaze.Repository` to
`IStorageRepository` in `TrailBlaze.Interface/Repository/`, the `AddBlobStorage` helper was deleted,
and `IStorageRepository` was moved out of `Interface/Infrastructure/`. A reader doing that work again
has no way to know that four members are waiting rather than abandoned.

**Tests currently pin the unreferenced surface.** [StorageContainerRoutingTests.cs](../../src/api/TrailBlaze.Repository.Test/StorageContainerRoutingTests.cs)
is a whole test file whose subject has no production consumer — it exercises `MoveAsync` and
`CreateReadUrlAsync` against a live account. So deleting `MoveAsync` as unused breaks a green test,
and the test's existence reads as evidence of use. Neither signal is about production.

*(Before 2026-09-18 that file was `StorageSeamTests.cs`, which pinned the same members by asserting
the arguments a fake recorded. It was deleted with the fake, and the claims were re-derived against
a real account — so the members are now pinned by observed outcomes rather than by call arguments.
The amount of green test protecting them is unchanged; what changed is that it is harder to write
and therefore less likely to be mistaken for production coverage.)*

**And the members encode design decisions for features that are not written.** `CreatePublicUrl`'s
remarks state that feature 07 *forbids* minting a SAS for a blob in a public container, which is why
it exists beside `CreateReadUrlAsync`. That constraint is real and load-bearing — but it is enforced
by prose in a doc comment, not by anything a caller does, and feature 07 is where it either holds or
turns out to have been the wrong shape.

## Evidence

Re-measured 2026-09-18, after the storage fake was deleted. The inventory is unchanged; two of the
paths that carried the evidence are not.

- `grep -rn "CreateReadUrlAsync" src/api --include=*.cs` returns the interface declaration and three
  doc-comment references around it, the repository's declaration and one doc reference, and **four
  test call sites** (two in `AzureBlobStorageIntegrationTests`, two in `StorageContainerRoutingTests`).
  **No production call site.**
- `grep -rn "MoveAsync" src/api --include=*.cs` returns the interface declaration, the repository
  implementation, and **one test call site** in `StorageContainerRoutingTests`. **No production call
  site.**
- `grep -rn 'StorageContainer\.\(Covers\|Media\)' src/api --include=*.cs` returns one doc comment in
  the interface and, beyond that, only test files — `StorageContainerRoutingTests`,
  `AzureBlobStorageIntegrationTests` and `AzureStorageFixture`. **No production file.**
- The interface itself documents the intended consumers by feature number — `Covers` "route a cover
  by the activity's `Type`" (feature 08), `Media` "reached only through a short-lived SAS URL"
  (features 06/07).
- The storage tier does exercise `CreateReadUrlAsync` — `AzureBlobStorageIntegrationTests` mints a
  URL, fetches it, and repeats the fetch after expiry — so the SAS logic is not unproven. It is
  unproven *in production use*. **Re-stamped 2026-09-18:** that tier's default target is now the
  **Azurite emulator**, which needs no credentials; a real Azure account is reached only when
  `TRAILBLAZE_STORAGE_CONNECTION` names one. So "exercised against a real account" is true of the
  code path and of the emulator's behaviour, and an assertion about **Azure's own** behaviour is the
  narrower claim the env override buys.

## Testability

**verification-only.** The claim under repair is an inventory of callers, which is a property of the
codebase rather than a behaviour. There is no assertion that could hold it — a test that read the
call graph would be testing the index, not the code.

The honest verification is a `grep` of the two production projects recorded in the PR body. If a
`doc-assertion` is wanted, it would have to pin a *comment*, which is worse than the comment.

## Repair plan

**Not to be repaired under this item, and it should not be closed by deleting anything.** The repair
is features 06–08 supplying the consumers. What this item owes now:

1. **Say it in the interface.** One line on each of the four members: which feature consumes it, and
   that it has no production caller yet. This is the whole fix — it converts "unused" into "waiting",
   which is the only distinction that was missing.
2. **Say it in the test file.** `StorageContainerRoutingTests.cs` should state that it is the seam's
   only consumer until features 06–08 land, so its greenness is not mistaken for production coverage.
   Its class comment already opens by recording that it *was* `StorageSeamTests` and why the claims
   had to be re-derived; the sentence this step asks for is not in it yet.
3. **Check whether `MoveAsync`'s and `CreateReadUrlAsync`'s signatures survive contact with their
   features.** They were designed from the PRD before the callers existed. If feature 07 needs a
   different lifetime parameter, or feature 08 needs the destination path computed differently, that
   is a finding — record it against the feature rather than working around it in the caller.
4. **Per-member closure.** Each of the four closes when its feature lands a production caller. The
   item closes when all four do. Do not close it on the comment alone.

## Out of scope / related

- **Deleting the members is not a repair.** It would break `StorageContainerRoutingTests` and remove
  the seam features 06–08 are designed to attach to.
- **Feature 07's public-container prohibition** is the constraint that makes `CreatePublicUrl` and
  `CreateReadUrlAsync` two methods rather than one parameterised one. If that requirement changes,
  both the interface and feature 07's doc are wrong together — that is a bug-shaped finding, not
  debt, because it would let a private blob be addressed without authorization.
- **`DeleteAsync` on a missing object is documented as not-an-error** and that behaviour *is*
  covered — `AzureBlobStorageIntegrationTests.A_delete_of_something_absent_is_not_an_error`, which
  is the claim the fake could never settle because `DeleteIfExists` is the real client's method. It
  has production callers, so it is not part of this item — noted only because a reader counting
  callers will hit it too.

## Close checklist

- [ ] Each of the four members states which feature consumes it and that it is currently unreferenced
- [ ] `StorageContainerRoutingTests.cs` states it is the seam's only consumer until features 06–08 land
- [ ] Each of the four has a production caller (`grep` recorded in the closing PR)
- [ ] Any signature that did not survive contact with its feature is recorded against that feature
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
