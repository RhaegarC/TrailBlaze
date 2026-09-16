# Backend Testing & TDD Strategy

Status: **Draft** (2026-09-15)

Referenced by the `tdd-implement` and `bug-fix` agents — the TDD workflow (RED → GREEN →
refactor) runs on the tiers below.

**Scope: the backend API** (`src/api`). The frontend UI is exported from Figma Make and is out of
scope for this TDD strategy — it is not test-first. The only hand-written frontend work is
API integration, verified manually end-to-end.

The API is a layered solution under `src/api/` — `TrailBlaze.Model`, `TrailBlaze.Repository`,
`TrailBlaze.Service`, `TrailBlaze.Interface`, `TrailBlaze.Api` — and each layer carries a sibling
xUnit test project (`TrailBlaze.Api.Test`, `TrailBlaze.Repository.Test`,
`TrailBlaze.Service.Test`). New tests go in the project matching the layer they exercise.

**Current state (2026-09-15).** The harness is in place. Each `*.Test` project references the
layer it exercises and `dotnet test` discovers tests in all three: 31 runnable, of which the one
tagged `Category=StorageIntegration` skips without credentials, leaving 30 passing by default.

`TestSupport/AuditHarness.cs` and `TestSupport/FakeUserContext.cs` live in
`TrailBlaze.Repository.Test`; `TestSupport/FakeStorageRepository.cs` lives in
`TrailBlaze.Service.Test`. The API tier boots the real pipeline through `WebApplicationFactory`
and supplies unreachable connection strings, so it needs no database either — nothing has to be
removed from the service collection to achieve that, because migrations are applied by the
deployment pipeline rather than at startup, and the context is not resolved until a request asks
for it.

## Test tiers

| Tier | Scope | Tooling | Runs |
|---|---|---|---|
| Backend unit | Services, **ownership/permission evaluation**, upload validation, SAS policy construction, pagination clamping | xUnit | Always — fast, offline |
| Backend integration | EF Core **with no database at all** — persistence, repositories, queries, cascade deletes | xUnit + EF Core | Always — offline |
| Storage integration | The real Azure Blob implementation of `IStorageRepository` — upload, delete, SAS round-trip | xUnit + Azure SDK | **Explicitly tagged**; requires credentials |

## Testing without a database

The repository tier needs **nothing listening on a port**. EF Core's save interception runs
before the provider opens a connection, so audit stamping, soft-delete filtering, and
application-assigned keys are all provable offline — and a LINQ query can be inspected with
`ToQueryString()` instead of executed.

The pattern is specified in [src/api/STANDARD.md](../src/api/STANDARD.md) §10. In outline: the
context is wired through the same `AddRepositoryPersistence` entry point the application uses, the
HTTP-backed `IUserContextService` is replaced with a fake so a test can set the caller, and the
repository is exercised against that. No container, no connection string, no credentials — which
is why this tier runs on every test invocation rather than on a CI-only branch.

This is what makes the tier *fast*, but not what makes it *trustworthy*: the fake user context is
a stand-in, so anything that depends on the real token pipeline still needs the E2E tier
(feature 11).

## The Azure dependency

Azure Blob is a **real cloud resource in every environment** (PRD Decision #5), which would
normally make the test suite slow, credentialed, and non-hermetic. The design contains this:

- All blob access goes through **`IStorageRepository`**.
- **Unit tests inject an in-memory fake.** They never touch the network. This is where the
  RED → GREEN loop lives, and it stays instant and offline.
- A small **storage integration tier** exercises the real account and is tagged so it can be
  excluded when credentials are absent. CI must hold Azure credentials for this tier to run.

The fake is for *unit* tests only. A test that asserts upload behavior while never leaving the
fake proves the caller's logic, not the blob implementation — so anything Azure-specific
(SAS generation, container existence, content-type round-tripping) belongs in the tagged tier.

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

## Commands

- Backend (from `src/api/`): `dotnet test`
- Storage integration tier only, with credentials in the environment:

  ```bash
  TRAILBLAZE_STORAGE_CONNECTION="<azure storage connection string>" \
    dotnet test --filter Category=StorageIntegration
  ```

  Without `TRAILBLAZE_STORAGE_CONNECTION` the test **skips** rather than fails, so `dotnet test`
  is green on a machine with no Azure account. A skip is reported in the run summary — it is a
  skip, not a silent exclusion, so the tier cannot be forgotten by vanishing from the output.
