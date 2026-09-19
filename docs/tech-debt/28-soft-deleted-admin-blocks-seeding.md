# 28 — A soft-deleted row blocks the configured admin from being seeded

Status: **open** · Kind: correctness · Impact: friction · Area: Seeding
Source: found 2026-09-19 (verifying feature 03's seeder against a populated database) · Discharges via: — (orphaned) · Opened: 2026-09-19 · Last verified: 2026-09-19

## What the debt is

The seeder looks up the configured object id through the repository, and the repository applies the
soft-delete filter like every other query. So a row that exists but is soft-deleted is **invisible**
to the lookup, and the seeder takes the branch that inserts:

| Step | What happens with a live row | What happens with a soft-deleted row |
|---|---|---|
| `GetAsync<User>(u => u.Id == objectId)` | finds it | finds nothing — the filter excludes it |
| Seeder's next move | promote, or report `AlreadyAdmin` | `CreateAsync` an insert |
| Engine's answer | n/a | `PK_Users` violation, SQL error 2627 |

`CreateAsync` throws, `AdminSeedingHostedService` catches broadly, logs at error, and starts anyway.
The result is a deployment that is **unadministrable on every start**, with one error line as the
only symptom, while the row that carries the privilege sits in the table one flag away from being
usable.

## Why it matters

**The failure is permanent and self-inflicted.** Nothing retries, nothing degrades to a working
state, and no request can repair it. Every restart produces the same PK violation, so the operator's
only signal is a line they will read once and then stop seeing.

**The host is healthy while it is unusable.** This is the same trade feature 03 made deliberately —
database trouble must not crash-loop a replica — and it is what turns a stuck state into a silent
one. The logged error is the entire detection story.

**It is not reachable from the product today, which is why it is filed rather than blocking.** No
code path deletes a user: there is no delete endpoint and no admin screen
([feature 03's non-goals](../features/03-admin-seeding.md#notes--non-goals)). So the state requires
someone to have set `IsDeleted` by hand — exactly the "edit a row in SQL" the seed path exists to
make unnecessary. It becomes reachable the moment a user-deletion feature lands.

## Evidence

Checked 2026-09-19, against the local development database with the `ConstrainUserRole` migration
applied.

- **Verified end to end, not reasoned about.** A row was planted for the configured object id with
  `IsDeleted = 1, Role = 'Admin'`, then the real API was booted with that id configured as the
  administrator. The log carried:

  ```
  Administrator seeding did not complete, so this instance has no administrator unless an earlier
  start seeded one. The host is starting anyway: ...
  Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes.
   ---> Microsoft.Data.SqlClient.SqlException: Violation of PRIMARY KEY constraint 'PK_Users'.
        Cannot insert duplicate key in object 'dbo.Users'.
        The duplicate key value is (probe-soft-deleted-admin).
  ```

  The host then reported its content root and shut down cleanly on the probe's kill — it was up and
  serving with no administrator in it. The planted row was unchanged afterwards
  (`Role=Admin IsDeleted=True`), and was removed once the probe finished.
- The mechanism is `DatabaseRepository.GetAsync`, a plain `Context.Set<T>().FirstOrDefaultAsync`,
  against the filter set for every `EntityBase` in `TrailBlazeContext.ApplySoftDeleteFilter`. No
  `IgnoreQueryFilters` anywhere on this path.
- The catch that swallows the failure is `AdminSeedingHostedService`'s, whose remarks argue for
  surviving an unreachable database. That argument is sound and does not cover this: a database that
  answers, with a row that is real but filtered, is not the case the tolerance was built for.

## Testability

**testable**, and the container tier already has everything it needs — the probe above is one test.
A `Category=Container` assertion would soft-delete a row for the configured id, run the seeder, and
assert whatever outcome the repair decides on. It belongs in `TrailBlaze.Service.Test` beside
`AdminSeedingTests`, by the precedent that item settled.

## Repair plan

1. **Decide the policy before writing the fix, because that is the actual question.** An operator who
   names an object id as the administrator has said something about that account; the options differ
   in whether the seeder may overrule a deletion.
   - *(a)* **Resurrect it.** Seed the row by un-deleting it and setting `Role = Admin`. Simple, and
     defensible on the grounds that the configuration is the operator's latest word — but it means
     startup silently reverses a deletion, and it needs a way to reach a filtered row.
   - *(b)* **Refuse to guess, and say so distinctly.** Keep the row deleted, and fail seeding with an
     outcome (or a log line) naming the situation: "a row exists for the configured administrator and
     is deleted; seeding cannot proceed". The operator then makes a decision instead of having one
     made for them. Costs a new `AdminSeedOutcome` member or a dedicated exception, and does not
     restore service on its own.
   - *(c)* **Leave it, and record it.** The state is unreachable from the product and the error line
     names the constraint. This item is then the record.
2. **Whatever is chosen, the lookup has to be able to see the row.** Today it cannot: either the
   repository grows a way to query unfiltered (`IDbRepository` exposes none, and
   `TrailBlazeContext` deliberately gestures at `IgnoreQueryFilters` as an explicit decision), or the
   seeder reads the duplicate-key failure from the insert — which means interpreting a provider
   exception in the service layer, the thing `AdminSeedingHostedService`'s remarks refuse to do in
   the Api layer.
3. **Do not widen the hosted service's catch to make the symptom nicer.** The failure is already
   logged; what is missing is a policy, not a stack trace.
4. **Re-check reachability when a user-deletion feature is written.** That feature is what promotes
   this from latent to live, and it should either close this item or inherit it explicitly.

**Recommendation: (b), and not until a deletion path exists.** (b) is the only option that neither
overrules an operator silently nor invents a policy this feature does not own, and its cost is a
distinct outcome the logs can carry. (a) is genuinely arguable and is the better answer if a
deletion path is never added; it is the wrong answer to reach for first, because "startup un-deletes
things" is a property a deployment should opt into rather than discover.

## Out of scope / related

- **[Item 05](05-soft-delete-recorded-as-modified.md)** is about how a soft delete is recorded in the
  audit table, not about filtered lookups. They share a root — `IsDeleted` has more readers than it
  has writers — and would be worth repairing in one pass.
- **[Feature 03](../features/03-admin-seeding.md)** made this trade knowingly and records it in its
  non-goals; this item is the detail behind that bullet, not a contradiction of it.
- **A user-deletion feature** does not exist. When it does, this item's reachability changes and it
  should be re-verified before being worked.

## Close checklist

- [ ] The policy is decided and written down, including whether a deletion may be reversed at startup
- [ ] The seeder can tell "no row" from "a row I cannot see", or the reason it need not is recorded
- [ ] A container test plants a soft-deleted row for the configured id and asserts the chosen outcome
- [ ] Feature 03's non-goal bullet points here
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
