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

The rule has no exception, and feature 03 settled the case that looked like one: a test whose subject
is service-layer logic but which needs a store still goes in `TrailBlaze.Service.Test`, which took a
`ProjectReference` to `TrailBlaze.Repository.Test` to reach `TestSupport/`'s fixtures. Filing store-
backed service assertions as "repository behaviour" would have put them in the tier whose name says
they are about the wrong layer — the failure the suffix rule exists to prevent
([codereview.md](../.claude/rules/codereview.md)). [Item 25](tech-debt/25-service-test-tier-is-empty.md)
records the decision, including the recommendation it overrode.

**Current state (2026-09-19).** Two containers back the suite — `azure-sql-edge` and
`azure-storage-edge`, started by [`docker-compose.test.yml`](../src/api/docker-compose.test.yml) —
and 76 tests are discovered: 60 in `TrailBlaze.Repository.Test`, 9 in `TrailBlaze.Service.Test`, 7 in
`TrailBlaze.Api.Test`. **With the containers running all 76 pass. With nothing configured, 38 pass
and 38 skip** — and that second number is the honest description of a bare machine, not a failure.
Measured, not derived: the run reports `Failed: 0, Passed: 38, Skipped: 38, Total: 76` across the
three projects.

**This paragraph is the only place those counts are written down, and that is deliberate.** They
were previously restated in the README, the PRD, the sprint file and STANDARD §10, and went stale in
all four whenever a feature added a test. If you are here to update them, update them here and stop —
[item 19](tech-debt/19-doc-indexes-drifted.md) is the record of what the duplication cost.

**But the count is not the coverage.** The one product slice that has shipped and is still untested
is feature 02's — profile routes, avatar upload and upload validator. Those tests were deliberately
deferred, and [item 12](tech-debt/12-feature-02-tests-deferred.md) tracks writing them and records
why the gap is the dangerous kind: an untested guard everyone believes is tested is worse than one
known to be untested. Feature 03 (2026-09-19) added the first coverage of product behaviour rather
than of the foundation — the `Role` constraint and its backfill, and the seeding and role-resolution
logic — so "the foundation is green" is no longer quite the whole story, but the profile slice still
is not covered.

`TestSupport/` lives in `TrailBlaze.Repository.Test` and holds the container fixtures,
`TestEnvironment`, and `FakeUserContext`. **There is no fake for storage and none for the
database**: the tier runs the real `AzureBlobStorageRepository` against a live account, and a real
`TrailBlazeContext` against a real engine. The API tier boots the real pipeline through
`WebApplicationFactory` and supplies unreachable connection strings, so it needs no database —
nothing has to be removed from the service collection to achieve that, because migrations are
applied by the deployment pipeline rather than at startup, and the context is not resolved until a
request asks for it.

Since feature 03 that is a claim the tier *defends* rather than one it simply enjoys. Seeding runs
in a hosted service, so booting now makes one connection attempt that is expected to fail; it is
caught and logged, and the host starts. The tier is therefore also the regression test for the
offline property itself — if a future change made a database failure fatal to startup, `StartupTests`
would go red on a machine with no container, which is the failure signal worth having.

## Test tiers

| Tier | Scope | Tooling | Runs |
|---|---|---|---|
| Backend unit | Upload validation, SAS policy construction, pagination clamping, ownership/permission evaluation once feature 09 lands, and reflection assertions that a value has no second source (`RoleComesFromTheRowTests`) | xUnit | Always — fast, offline |
| Repository model | EF Core's *model* and its *generated SQL* — keys, column types and lengths, soft-delete predicates read through `ToQueryString()` | xUnit + EF Core | Always — offline, opens no connection |
| Database | The real engine: the migration set applies, the fluent bounds reached `INFORMATION_SCHEMA`, a duplicate key collides, the soft-delete filter executes, audit JSON round-trips, a narrowing `ALTER COLUMN` is refused, a check constraint refuses a value outside its set, a default fills itself in | xUnit + SQL Edge | **`Category=Container`** — skips when unreachable |
| Storage | The real `AzureBlobStorageRepository`: upload, content-type round-trip, a minted SAS that the server accepts, public/private routing, a move | xUnit + Azure SDK | **`Category=Container`** — skips when unreachable |
| Api host | The real pipeline through `WebApplicationFactory` with unreachable connection strings — a missing setting stops startup and the message names the key, and every registration in the composition root resolves | xUnit + `WebApplicationFactory` | Always — no database, deliberately |

`Category=Container` is **the only trait in the solution**. That makes the two obvious filters easy
to misread: `--filter "Category!=Container"` is not "the offline run", it is the 27 tests that touch
*neither* container — which excludes the 11 storage tests, and those run on a bare machine too,
because storage falls back to the emulator and needs no secret. The bare-machine run is plain
`dotnet test`, which is 38 passed and 38 skipped.

## What still runs offline, and why it is worth keeping

The repository model tier compares the model to its snapshot and inspects generated SQL with
`ToQueryString()`, which builds the statement and stops. That is two in-memory artefacts compared to
each other: it proves what EF *would* send, and it cannot prove the engine accepts it. It earns its
place by being the tier that catches a fluent-configuration change in under a second, and by failing
on a machine with no container at all.

Which is exactly why the database tier exists. A `ToQueryString()` assertion and an executed one are
different claims, and the second is the one that catches a migration that never applied.

**One claim this document used to make has been withdrawn rather than tested.** An earlier version
of this table listed *cascade deletes* under the offline repository tier. The model has no foreign
keys — `AuditLog` is deliberately not an `EntityBase`, and nothing else declares a relationship — so
there is nothing to cascade, and the row was describing a property the schema does not have. It is
removed rather than satisfied by inventing a test for a behaviour that does not exist.

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

### Isolation

Each test class gets **one database of its own**, created and migrated in a fixture, dropped on
dispose. Not one shared database, and not a transaction rolled back per test: a read-back through the
*same* context hits EF's change tracker rather than the database, and a genuine second context needs
a second connection inside one transaction, which needs MSDTC — absent from a Linux SQL Edge
container. Row-level cleanup was the other candidate and is worse: this repo has no hard delete
(`IDbRepository.DeleteAsync` is a *soft* delete), so it would mean raw SQL ordered by foreign key,
extended by every future feature, rotting silently.

`Migrate()`, never `EnsureCreated()`. `EnsureCreated` builds the schema from the model and bypasses
`Migrations/` entirely, so a broken migration stays broken and the test still passes — a test that
cannot fail.

A test that must build its own schema — `MigrationNarrowingTests` drives a database to an earlier
migration by hand — gets a database per *test*, because a database cannot be returned to an earlier
migration once anything has taken it to head.

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
- **Upload validation** — content-type allowlist, size caps, and per-activity count cap, each
  with a rejection test at the boundary.

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

A verified claim and a tested claim are different strengths of claim, and the
[debt register](tech-debt/00-debt-log.md) labels every item with which of the three applies rather
than letting them read alike.

## Commands

From `src/api/`:

```bash
docker compose -f docker-compose.test.yml up -d    # reads MSSQL_SA_PASSWORD from .env
dotnet test                                        # everything runnable here
dotnet test --filter "Category=Container"          # the container tiers alone
dotnet test --filter "Category!=Container"         # the 20 tests touching neither container
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
