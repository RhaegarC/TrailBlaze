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

## Test tiers

| Tier | Scope | Tooling | Runs |
|---|---|---|---|
| Backend unit | Services, **ownership/permission evaluation**, upload validation, SAS policy construction, pagination clamping | xUnit | Always — fast, offline |
| Backend integration | EF Core against SQL Server (`Testcontainers` mssql) or InMemory for fast CI; repositories, queries, cascade deletes | xUnit + EF Core | Always (container) or CI |
| Storage integration | The real Azure Blob implementation of `IStorageService` — upload, delete, SAS round-trip | xUnit + Azure SDK | **Explicitly tagged**; requires credentials |

## The Azure dependency

Azure Blob is a **real cloud resource in every environment** (PRD Decision #5), which would
normally make the test suite slow, credentialed, and non-hermetic. The design contains this:

- All blob access goes through **`IStorageService`**.
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
- Storage integration tier only: `dotnet test --filter Category=StorageIntegration`
