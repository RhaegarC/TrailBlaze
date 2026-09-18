# 26 — The real composition root has never been asked to open a connection

Status: **open** · Kind: test-gap · Impact: friction · Area: Api
Source: found 2026-09-18 (during the container-backed test change) · Discharges via: — (orphaned) · Opened: 2026-09-18 · Last verified: 2026-09-18

## What the debt is

Two tiers assert different halves of the same registration, and neither does what the application
does.

| Tier | How it builds persistence | Opens a connection? |
|---|---|---|
| `TrailBlaze.Api.Test` | `Program`'s own composition root, with an unreachable connection string | **no** — resolving a `DbContext` does not connect |
| `TrailBlaze.Repository.Test` | `TestPersistence.Build`, a *second entry point* onto `AddRepositoryPersistence` | yes — against SQL Edge |

So `ServiceExt`'s registration is exercised twice and end-to-end at neither: the API tier proves it
can be *constructed*, the repository tier proves the extension method works against a live engine,
and **nothing proves that the composition root's own wiring can read a row**. A lifetime mistake — a
`DbContext` held longer than its scope, a singleton capturing a scoped service — is invisible to
`GetRequiredService`, which is all the API tier asks of it.

## Why it matters

**The unproven path is the one that runs in production.** The repository tier builds its context
through `TestPersistence`, which is a different caller with a different scope arrangement; it shares
the extension method with the application and nothing else. A registration error introduced in
`ServiceExt` that both tiers satisfy — because both construct rather than use — would first appear
on a deployed request.

**Nothing is wrong today, which is why it is debt.** Every registration resolves; the tiers are green;
the gap is in what has been *asked*, not in what the code does. `CompositionRootTests`' own remarks
say so plainly, and the leak this item names is hypothetical until someone makes the mistake.

**The fix is not free, and that is the whole reason it is filed rather than done.** Closing it means
giving the Api tier a database dependency, which it was deliberately built without: it boots the real
pipeline with unreachable strings so that `StartupTests` can assert host behaviour on a machine with
no container. That property is worth keeping, so the repair is a design question rather than a
missing assertion.

## Evidence

Checked 2026-09-18.

- `TrailBlazeApiFactory.UnreachableConnection` points at `127.0.0.1,1`; `CompositionRootTests`
  resolves five registrations and never opens the context.
- `TrailBlaze.Repository.Test/TestSupport/TestPersistence.Build` is the only other caller of
  `AddRepositoryPersistence`, and it is not the composition root.
- `TrailBlazeApiFactory`'s remarks and `CompositionRootTests`' remarks both record the boundary,
  the latter under the heading "What that leaves unproven, said plainly".
- The container tier's tests reach a server through their own fixtures, not through `Program`.

## Testability

**testable**, but only once the shape question below is answered — the assertion is "a request through
the real composition root reads and writes a row", which needs a reachable database from the Api
tier.

## Repair plan

1. **Decide how the Api tier reaches an engine, because that is the actual decision.**
   - *(a)* **A tagged test in `TrailBlaze.Api.Test`** that boots the factory with the container's
     connection string when one is available and skips otherwise. Faithful and narrow; costs this
     project a `Category=Container` test, a reference to the repository tier's fixtures or a copy of
     them, and the credentials path.
   - *(b)* **Move the claim to the repository tier** by having it build through the composition root
     rather than through `TestPersistence`. Cheaper, but `TrailBlaze.Repository.Test` does not
     reference `TrailBlaze.Api` — deliberately, since referencing upward inverts the layering.
   - *(c)* **Leave it, and record it.** The composition root is thin and reviewed; the mitigation is
     the existing remarks.
2. If (a): the test asserts a round trip through a real request path, not a resolution, and it must
   **skip** rather than fail when no container answers — the rule the rest of the tier follows.
3. Whatever is chosen, keep `StartupTests`' offline property intact. It is the reason this tier has
   no database, and it must not be traded away for this assertion.

**Recommendation: (a), and only when a request path exists that can be exercised without a bearer
token.** Until feature 09 provides test-token infrastructure, every `UserController` route is
`[Authorize]` with no scheme registered, so a real request is a 401 and the round trip cannot be
made. Doing (a) today would assert a resolver, which is the thing that is already proven.

## Out of scope / related

- **Feature 09 owns test-token infrastructure**, and it is the prerequisite for any end-to-end
  request test on the Api tier. Until it lands, this item is blocked in practice.
- **[Feature 11](../features/11-e2e-verification.md)** is the tier that runs the whole stack against
  real resources; it covers the deployed shape, not the composition root's wiring specifically.

## Close checklist

- [ ] Either a request through the real composition root reads a row, or (c) is recorded with a reason
- [ ] `StartupTests` still runs with no container
- [ ] The new assertion skips, rather than fails, when nothing answers
- [ ] The two tiers' remarks still describe what each does and does not prove
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
