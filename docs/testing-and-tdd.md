# Backend Testing & TDD Strategy

Status: **Draft** (2026-09-18)

Referenced by the `tdd-implement` and `bug-fix` agents — the TDD workflow (RED → GREEN →
refactor) runs on the tiers below.

**Scope: the backend API** (`src/api`). The frontend UI is exported from Figma Make and is out of
scope for this TDD strategy — it is not test-first. The only hand-written frontend work is
API integration, verified manually end-to-end.

The API is a layered solution under `src/api/` — `TrailBlaze.Model`, `TrailBlaze.Repository`,
`TrailBlaze.Service`, `TrailBlaze.Interface`, `TrailBlaze.Api` — and each layer carries a sibling
xUnit test project (`TrailBlaze.Api.Test`, `TrailBlaze.Repository.Test`,
`TrailBlaze.Service.Test`). New tests go in the project matching the layer they exercise.

**A test belongs to the tier that owns its claim kind.** A claim *decided by the request* — the field
rules, the request shape, what the service hands the repository — runs offline in
`TrailBlaze.Service.Test`. A claim *about what the store does* — the column type, the check
constraint, the soft-delete filter, the audit stamp — runs in `TrailBlaze.Repository.Test`, container
backed where it must be. No test project references another's, and no fake database stands in for
either.

**What that leaves is a seam.** No tier drives a service → `IDbRepository` → SQL Server in one run, so
a service's wiring is asserted against a recording double and the store's behaviour through direct
repository calls, and neither test would catch the two being wired to each other wrongly. Nothing in
the suite closes that seam today.

**Which product behaviour the suite covers, and which it does not, is status rather than standard.**
It is written down once, in [00-mission-1-sprint.md](features/00-mission-1-sprint.md).
This document says how a test is written and where it runs:
it names no feature and carries no count, because a count restated here went stale here every time a
feature added a test.

`TestSupport/` lives in `TrailBlaze.Repository.Test` and holds the container fixtures,
`TestEnvironment`, and `FakeUserContext`. **There is no fake for storage and none for the
database**: the tier runs the real `AzureBlobStorageRepository` against a live account, and a real
`TrailBlazeContext` against a real engine. The API tier boots the real pipeline through
`WebApplicationFactory` and supplies unreachable connection strings, so it needs no database —
nothing has to be removed from the service collection to achieve that, because migrations are
applied by the deployment pipeline rather than at startup, and the context is not resolved until a
request asks for it.

Nothing in the Api project reaches a store, so that tier is offline by construction rather than by
assertion.

## Test tiers

| Tier | Scope | Tooling | Runs |
|---|---|---|---|
| Backend unit | Rules decided from a request alone: upload validation, field validation, SAS policy construction, pagination clamping, ownership and permission evaluation, and reflection assertions that a value has no second source or no way in | xUnit | Always — fast, offline |
| Repository model | EF Core's *model* and its *generated SQL* — keys, column types and lengths, soft-delete predicates read through `ToQueryString()`, a check constraint's SQL, and the absence of a relationship the docs might imply | xUnit + EF Core | Always — offline, opens no connection |
| Database | The real engine: the migration set applies, the fluent bounds reached `INFORMATION_SCHEMA`, a duplicate key collides, the soft-delete filter executes, audit JSON round-trips, a narrowing `ALTER COLUMN` is refused, a check constraint refuses a value outside its set, a default fills itself in, a row round-trips through `DatabaseRepository` and is re-read in a fresh scope; and the database it creates outlives the run | xUnit + SQL Edge | **`Category=Container`** — skips when unreachable |
| Storage | The real `AzureBlobStorageRepository`: upload, content-type round-trip, a minted SAS that the server accepts, public/private routing, a move | xUnit + Azure SDK | **`Category=Container`** — skips when unreachable |
| Api host | The real pipeline through `WebApplicationFactory` with unreachable connection strings — a missing setting stops startup and the message names the key, every registration in the composition root resolves, and a protected route answers 401 to an anonymous caller once Entra is configured | xUnit + `WebApplicationFactory` | Always — no database, deliberately |

`Category=Container` is **the only trait in the solution**, which makes the two obvious filters easy
to misread: `--filter "Category!=Container"` is not "the offline run", because it also excludes the
storage tier — which runs on a bare machine too, since storage falls back to the emulator and needs
no secret.

A protected route needs a configured tenant and audience, or the host answers **500 rather than 401**:
with no scheme there is nothing to challenge with. That is a defect rather than a design, and no test
catches it today.

## What still runs offline, and why it is worth keeping

The repository model tier compares the model to its snapshot and inspects generated SQL with
`ToQueryString()`, which builds the statement and stops. That is two in-memory artefacts compared to
each other: it proves what EF *would* send, and it cannot prove the engine accepts it. It earns its
place by being the tier that catches a fluent-configuration change in under a second, and by failing
on a machine with no container at all.

Which is exactly why the database tier exists. A `ToQueryString()` assertion and an executed one are
different claims, and the second is the one that catches a migration that never applied.

**Cascade deletes are not a tier here, because there is nothing to cascade.** The model declares no
foreign keys — `AuditLog` is deliberately not an `EntityBase`, and nothing else declares a
relationship — so the claim is withdrawn rather than satisfied by inventing a test for a behaviour
the schema does not have.

**Nothing asserts that a save to an unreachable port fails.** That assertion was a tripwire proving
the offline tier had not silently stopped testing; it held because save interception runs before a
connection opens, but it is an EF ordering guarantee rather than a TrailBlaze behaviour and is
unobservable against a live server. The skip does that job instead, and is louder
([STANDARD.md](../src/api/STANDARD.md) §10).

## The container tier

### Skipping, not failing

Reaching nothing at the configured endpoint **skips**; a container that is simply not running is not
a bug in the code under test. Everything past the connect check is a **failure**, deliberately — a
migration that will not apply against a real engine is the exact bug this tier exists to find, and
degrading it to a skip would hide the one thing it was built for.

`Skip.IfNot` works by throwing, which trips three things worth knowing before writing a test here:

- It requires `[SkippableFact]` / `[SkippableTheory]`. Under a plain `[Fact]` the thrown
  `SkipException` is just an exception and the test **fails**.
- It must be called **outside** anything that catches. `Record.ExceptionAsync` and
  `Assert.ThrowsAsync` catch the `SkipException` too and hand it back as the exception under test, so
  the test reports a failure whose "expected" value is the skip message. Resolve the fixture's client
  or repository before the recorder.
- The fixtures skip for you. Branching on `IsAvailable` by hand is how a test ends up either not
  skipping or skipping for a reason it never states.

### One database, and one per test that needs its own

**Every container test works against the same database, `TrailBlazeTest`.** It is created once per run
and migrated to head; each test class gets a scope over it, not a database of its own. Isolation is
therefore by row rather than by database: an assertion here is scoped to the id it wrote, and nothing
may assert on a table as a whole. A transaction rolled back after each test was the obvious
alternative and does not work: reading a row back through the *same* context hits EF's change tracker
rather than the database, and a genuine second context needs a second connection inside one
transaction, which needs MSDTC — absent from a Linux SQL Edge container. Row-level cleanup was the
other candidate and is worse: this repo has no hard delete (`IDbRepository.DeleteAsync` is a *soft*
delete), so it would mean raw SQL ordered by foreign key, extended by every future feature, rotting
silently.

A test that must drive a schema of its own — `MigrationNarrowingTests` stops one at an earlier
migration — cannot use it: that state may not leak into the next test, and a database cannot be
returned to an earlier migration once anything has taken it to head. Those tests get a
`TrailBlazeScratch_<guid>` database each, dropped when the test that made it finishes.

### Retention

**The run's database is not dropped at teardown.** A failing container-backed assertion is diagnosed
by reading the rows behind it, so `TrailBlazeTest` is left standing — tables, rows and all. The next
run drops it before creating its own, so one run's worth survives and the engine does not fill up
with databases. A scratch database is the exception, and goes with the test that made it, which is
what keeps the run's leavings to the one database it means to leave.

The pair on the engine is then always the same two:

```sql
SELECT name, create_date FROM sys.databases WHERE name LIKE 'TrailBlaze%'
-- TrailBlaze      the local dev database (README, "Running it locally")
-- TrailBlazeTest  this tier
```

`TestDatabaseRetentionTests` asserts both halves — that the run's database outlives its disposal, and
that a test's own does not.

**Inspect before `docker compose down`.** `azure-sql-edge` has no volume, so removing the container
takes `TrailBlazeTest` with it. Stopping it, or leaving it running, keeps it.

`Migrate()`, never `EnsureCreated()`. `EnsureCreated` builds the schema from the model and bypasses
`Migrations/` entirely, so a broken migration stays broken and the test still passes — a test that
cannot fail.

The migration set is applied once per run, by the same call that creates the database. Running it
from each fixture instead would execute `Migrate()` four times over one database, from four classes
running in parallel — and the loser of a race over `__EFMigrationsHistory` fails a create it did not
need.

### Storage

One account for the whole tier, and containers created rather than assumed. Objects are GUID-keyed
and the containers are shared in production, so there is nothing to isolate.

The tier runs against the **Azurite emulator by default**, which needs no credentials at all, and
against a real account when `TRAILBLAZE_STORAGE_CONNECTION` names one. Both reach the same code;
only the account behind it differs. That is what makes this tier runnable on any machine rather than
on a credentialed CI branch.

Provisioning includes setting each container's access level — but **only when the endpoint is
loopback**. A test run must never change the access level of a container in a real account: that is a
deployment change with a security consequence, made silently, by a process whose whole job is to
observe.

## The Azure dependency

Azure Blob is a **real cloud resource in every environment** (PRD Decision #5). The design contains
that by routing all blob access through `IStorageRepository`, so the vendor is confined to one file
and the contract names no Azure type.

**There is no fake implementation of `IStorageRepository`, and its absence is deliberate.** A deleted
in-memory fake once stood here; it implemented the contract it was asserting, which meant a test
asserting "upload then read back" proved only that a dictionary tolerates a key. A fake shadows the
real implementation's invariants while appearing to test them. Anything Azure-specific — SAS
generation, container existence, content-type round-tripping — is now proven against a live account
or not at all.

What that costs is real and is stated rather than hidden: the RED → GREEN loop for storage-facing
code is no longer instant, and the tier skips on a machine with no emulator. What it buys is that no
storage assertion in this suite is an assertion about a fake.

## TDD discipline

1. **RED** — write a failing test for the behavior first; run it to confirm it fails for the right reason.
2. **GREEN** — minimal implementation to pass.
3. **Refactor** while green; run the full tier.

**Must be test-first (hot spots):**
- **Ownership and permission evaluation** — the security boundary of the whole app: owner-only
  edit/delete, admin override, and anonymous denial on exactly the two public read endpoints.
- **SAS URL issuance** — that an unauthenticated or unauthorized caller is rejected *before* any
  blob operation happens, and that expiry is bounded.
- **Upload validation** — content-type allowlist, size caps, and the per-contributor count cap,
  each with a rejection test at the boundary.

These are the places where a passing test suite is the only evidence the app is not quietly
serving private media to the wrong person.

### When there is no behaviour to drive

A correct change does not always have runtime behaviour to test. Two further shapes are sanctioned,
so that such a change is not forced into a test that cannot fail
([STANDARD.md](../src/api/STANDARD.md) §10):

- **`doc-assertion`** — the change protects a durable property of a *file*: "every command is listed
  in the README", "no permission entry names another repository". Write a test that reads the file
  and asserts the property, and confirm it goes red when the file is reverted. It is a real test with
  a real failure mode.
- **`verification-only`** — nothing can be asserted at all: a workflow file, a deployment step, a
  request line in a `.http` file. Record what was run and observed in a `Verification:` line, and say
  in the pull request why there is no test. **Do not invent a test that cannot fail** to make the
  change look finished.

A verified claim and a tested claim are different strengths of claim, and blurring them is worse than
either.

## Commands

From `src/api/`:

```bash
docker compose -f docker-compose.test.yml up -d    # reads MSSQL_SA_PASSWORD from .env
dotnet test                                        # everything runnable here
dotnet test --filter "Category=Container"          # the container tiers alone
dotnet test --filter "Category!=Container"         # the tests touching neither container
docker compose -f docker-compose.test.yml down
```

`.env` is gitignored; [`.env.example`](../src/api/.env.example) is tracked and holds the shape
without the secret. The SA password reaches the tests two ways — the compose file reads
`MSSQL_SA_PASSWORD` from `.env`, and `TestEnvironment` composes a connection string from the same
variable — so one exported value serves both.

The test variables are named `TRAILBLAZE_SQL_CONNECTION` and `TRAILBLAZE_STORAGE_CONNECTION` rather
than the application's `DbConnection`/`BlobConnection`, and that is load-bearing: a developer with a
working API setup has the latter in their shell already, pointing at a real Azure SQL Database. A
test tier that read them would have `dotnet test` create and drop databases in production.

A skip is reported in the run summary. It is a skip, not a silent exclusion, so a tier cannot be
forgotten by vanishing from the output.
