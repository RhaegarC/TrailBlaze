# 01 — Foundation

Status: **Implemented** — awaiting PR review · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #5/#6/#16/#22 + "System overview" and "Deployment".

## Summary

The one-time scaffold every later feature starts from: the layered `TrailBlaze.*` solution with a
sibling xUnit project per layer, EF Core against **Azure SQL Database** with migrations the
deployment pipeline applies, a `Dockerfile` that builds the API into the image Azure Container
Apps deploys, the
`IStorageService` abstraction with an in-memory fake, OpenAPI, a `/health` endpoint, and
configuration binding for the Azure Blob connection string. It ships no product behaviour — its
exit condition is a green `dotnet test` **that actually runs tests**.

### Current state (2026-09-15)

The repository was initialised from a generic layered .NET scaffold, so part of this feature
already existed and part did not. Both gating items are now closed:

| Already present (verified, not rebuilt) | Delivered by this feature |
|---|---|
| The five layers under `src/api/`, flat, with `TrailBlaze.slnx` at that level | The three `*.Test` projects now reference the layer each exercises; `dotnet test` discovers 31 tests |
| EF Core registered through `AddRepositoryPersistence`; the connection string read from configuration | Provider swapped to `Microsoft.EntityFrameworkCore.SqlServer`; migrations and snapshot regenerated |
| `GET /health` and the OpenAPI document (development only) | `src/api/Dockerfile` plus `.dockerignore` — the image ACA deploys |
| `EntityBase`, `AuditSaveChangesInterceptor` and the soft-delete query filter | `IStorageService` with the three containers, plus the in-memory fake |
| Entra bearer validation and caller auto-provisioning — that is feature 02's subject; see [02-entra-auth.md](02-entra-auth.md) | `BlobConnection` configuration key, and startup validation for the required settings |

Two items gated every other feature: **the test harness** and **the provider swap**. Until the
harness ran a real assertion, no later feature had a RED step to begin from; until the provider
was Azure SQL Server, every migration written here was shaped for the wrong database.

## Story

As a backend developer I want a building, testable, containerised skeleton of the layered API so
that every later feature begins from a failing test rather than from project setup.

## Dependencies

- None. This is the first feature.

## Acceptance criteria

### Already satisfied — verify, do not rebuild

- [x] `src/api/` contains `TrailBlaze.Api`, `TrailBlaze.Interface`, `TrailBlaze.Model`,
      `TrailBlaze.Repository`, `TrailBlaze.Service`, each with a sibling `TrailBlaze.<Layer>.Test`
      xUnit project, all listed in `src/api/TrailBlaze.slnx`
- [x] Project references run in one direction only (Api → Service → Repository → Model; Api and
      Service → Interface); no layer references a layer above it
- [x] Configuration is read at the composition root, and locally-supplied values come from
      user-secrets — no secret value is committed
- [x] OpenAPI is served in development and enumerates the API's routes
- [x] `GET /health` returns 200 without a token

### Outstanding

- [x] Each `TrailBlaze.<Layer>.Test` project references the layer it exercises, and `dotnet test`
      from `src/api/` **discovers at least one test per project**. A green run over zero tests does
      not satisfy this criterion — that is the current state, and it is the thing being fixed
- [x] The repository tier follows the no-database pattern of
      [STANDARD.md](../../src/api/STANDARD.md) §10: the context is wired through
      `AddRepositoryPersistence`, `IUserContextService` is replaced with a fake so a test can set
      the caller, and no test opens a connection ([testing-and-tdd.md](../testing-and-tdd.md))
- [x] `TrailBlaze.Api` registers EF Core against **Azure SQL Server** through a `DbContext` whose
      connection string comes from configuration, not from a literal (PRD Decision #16) — the
      inherited `Npgsql` provider is replaced and the existing migrations and model snapshot are
      regenerated for the new provider
- [x] EF Core migrations are **applicable to the deployed database without booting the API**: the
      design-time factory resolves `DbConnection` from the environment and refuses to guess, so
      `dotnet ef database update` in the deployment pipeline targets exactly the database it was
      pointed at. Migrations are deliberately **not** applied at API startup — see the note below
- [x] `src/api/Dockerfile` builds the API into the container image **Azure Container Apps**
      deploys, with `.dockerignore` keeping local `bin/`/`obj/` output out of the build context.
      There is deliberately **no `docker-compose.yml`**: the web app is deployed to Azure Static
      Web Apps by its own GitHub workflow and never joins a local stack, so there is no
      multi-service stack for compose to orchestrate. Local development is `dotnet run` with
      user-secrets, and the database is a real Azure SQL Database in every environment
- [x] `IStorageService` is declared in `TrailBlaze.Interface` with the operations the media
      features need (upload, delete, mint a read URL, and **move** — copy to a second container plus
      delete the source, which feature 08's visibility change requires); no call site names a
      concrete Azure type
- [x] The abstraction addresses **three** containers, named rather than hard-coded at call sites:
      `covers` (public), `avatars` (public) and `media` (private, SAS-only). The container is a
      parameter of the operation, because the destination is a *decision* — feature 08 routes a
      cover by the activity's `Type`, and feature 02 writes avatars to their own container. The
      container name is the whole of the public/private answer, so the set closes here
- [x] An in-memory `IStorageService` fake exists in test support and is what
      `TrailBlaze.Service.Test` injects; unit tests make no network call (PRD Decisions #5/#6). The
      fake **records the container** each call asked for, which is what lets 02, 06 and 08 assert
      "the right container" at unit level
- [x] Configuration binds an Azure Blob connection string alongside the existing Entra `TenantId`
      and `Audience`; note that **no client secret is needed** — validating inbound tokens uses
      only the public signing keys the authority serves (see [02-entra-auth.md](02-entra-auth.md))
- [x] A missing required setting fails startup with a message naming the setting, rather than
      booting half-configured. `AllowCORS` already does this for `AllowedOrigins`; `DbConnection`
      is currently accepted empty, which must not survive this feature

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`): fake `IStorageService` round-trip — upload returns a path, the
  fake holds the bytes, delete removes them; a service depending on `IStorageService` resolves
  against the fake and completes with the network unavailable. The load-bearing assertion here is
  that unit tests bind the fake, not Azure (PRD Decision #6).
- Integration (`TrailBlaze.Repository.Test`): the context is constructed with no database at all —
  save interception runs before a connection opens — and asserts that audit stamping, the
  soft-delete filter, and application-assigned keys behave; the migration set is checked by
  asserting the model reports no pending changes, with the generated SQL inspected via
  `ToQueryString()` rather than executed ([testing-and-tdd.md](../testing-and-tdd.md)).
- Integration (`TrailBlaze.Api.Test`): the app booted via `WebApplicationFactory` serves
  `GET /health` as 200 with no `Authorization` header present, and startup fails loudly when a
  required setting is absent.
- Storage integration (tagged `Category=StorageIntegration`) — **requires credentials, excluded
  when absent**: the real Azure implementation of `IStorageService` reaches the account and
  round-trips an upload. Only this tier proves the blob implementation; the fake proves callers.

## Notes / non-goals

### Migrations are applied by the pipeline, not at startup

The obvious design — an `IHostedService` that calls `MigrateAsync` before the host serves traffic —
was built and then removed, because it does not survive the target platform. **Azure Container Apps
runs several replicas**, and a deploy or scale-out starts more than one at once; every replica
would run `MigrateAsync` against the same database. Both read `__EFMigrationsHistory`, both decide
the migration is pending, both apply the DDL — and one fails with "there is already an object named
…", or the history insert collides, or the DDL deadlocks. It is intermittent, it only happens at
deploy time, and it presents as a flaky release rather than as a schema bug.

So schema application moves out of the application and into the deployment pipeline: the workflow
runs `dotnet ef database update` against Azure SQL **before** the new revision takes traffic, and
exactly one writer ever touches the schema. The consequence to be honest about is that until those
workflows exist, applying a migration is a **manual** step — `DbConnection="…" dotnet ef database
update` — because nothing else does it.

The hazard this creates and the reason the design-time factory throws rather than defaulting: with
`dotnet ef` now the only thing that migrates, a factory that fell back to a local connection string
would migrate the *wrong database* and report success.

- No roles, no activity or media tables, and no route beyond `/health` and `/user/me` — those
  arrive in 02 onward. Entra bearer validation and caller auto-provisioning already exist in the
  scaffold, so 02's remaining work is narrower than its own file implies; see
  [02-entra-auth.md](02-entra-auth.md).
- Azure Blob and Entra ID are **real cloud resources in every environment**, development and tests
  included (PRD Decision #5). The in-memory fake is a unit-test seam, not a way to run the app
  without Azure.
- No CI pipeline definition, and no deployment pipeline. The `Dockerfile` builds the image Azure
  Container Apps runs, but nothing builds or ships it yet; the GitHub workflows that deploy the
  API and the web app are not written (PRD "Deployment", STANDARD §11).
- No frontend scaffold. The React app is a Figma Make export consumed at feature 10 (PRD
  Decisions #17/#22); nothing under `src/web/` is authored here.
- No shared-kernel libraries, no CQRS/mediator pipeline, no repository-of-repository abstractions
  beyond what a layer boundary requires.
