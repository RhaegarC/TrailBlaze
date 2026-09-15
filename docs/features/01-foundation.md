# 01 — Foundation

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #5/#6/#16/#22 + "System overview" and "Deployment (stage 1)".

## Summary

The one-time scaffold every later feature starts from: the layered `TrailBlaze.*` solution with a
sibling xUnit project per layer, EF Core against SQL Server with migrations applied at startup, a
`docker-compose` that brings up the API and a SQL Server container together, the `IStorageService`
abstraction with an in-memory fake, OpenAPI/Swagger, a `/health` endpoint, and configuration
binding for the Azure Blob connection string and the Entra tenant/client ids. It ships no product
behaviour — its exit condition is a green `dotnet test`.

## Story

As a backend developer I want a building, testable, containerised skeleton of the layered API so
that every later feature begins from a failing test rather than from project setup.

## Dependencies

- None. This is the first feature.

## Acceptance criteria

- [ ] `src/api/` contains `TrailBlaze.Api`, `TrailBlaze.Interface`, `TrailBlaze.Model`,
      `TrailBlaze.Repository`, `TrailBlaze.Service`, each with a sibling `TrailBlaze.<Layer>.Test`
      xUnit project
- [ ] Project references run in one direction only (Api → Service → Repository → Model; Api and
      Service → Interface); no layer references a layer above it
- [ ] `dotnet build` and `dotnet test` both succeed from `src/api/` on a clean checkout
- [ ] `TrailBlaze.Api` registers EF Core against SQL Server and a `DbContext` whose connection
      string comes from configuration, not from a literal (PRD Decision #16)
- [ ] EF Core migrations are applied automatically at API startup against the configured database
- [ ] `docker-compose` brings up the API container and the SQL Server container together, and the
      API reaches the database by service name rather than by `localhost`
- [ ] `IStorageService` is declared in `TrailBlaze.Interface` with the operations the media
      features need (upload, delete, mint a read URL); no call site names a concrete Azure type
- [ ] An in-memory `IStorageService` fake exists in test support and is what `TrailBlaze.Service.Test`
      injects; unit tests make no network call (PRD Decisions #5/#6)
- [ ] Configuration binds an Azure Blob connection string plus the Entra tenant id and client id;
      locally these are supplied from user-secrets, and no secret value is committed
- [ ] A missing required setting fails startup with a message naming the setting, rather than
      booting half-configured
- [ ] OpenAPI/Swagger is served in development and enumerates the API's routes
- [ ] `GET /health` returns 200 without a token

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`): fake `IStorageService` round-trip — upload returns a path, the
  fake holds the bytes, delete removes them; a service depending on `IStorageService` resolves
  against the fake and completes with the network unavailable. The load-bearing assertion here is
  that unit tests bind the fake, not Azure (PRD Decision #6).
- Integration (`TrailBlaze.Repository.Test`): a `DbContext` pointed at a SQL Server container
  (Testcontainers `mssql`) migrates from an empty database and reports no pending model changes.
- Integration (`TrailBlaze.Api.Test`): the app booted via `WebApplicationFactory` serves
  `GET /health` as 200 with no `Authorization` header present.
- Storage integration (tagged `Category=StorageIntegration`) — **requires credentials, excluded
  when absent**: the real Azure implementation of `IStorageService` reaches the account and
  round-trips an upload. Only this tier proves the blob implementation; the fake proves callers.

## Notes / non-goals

- No authentication middleware, no roles, no activity tables, and no route beyond `/health` —
  those arrive in 02 onward.
- Azure Blob and Entra ID are **real cloud resources in every environment**, development and tests
  included (PRD Decision #5). The in-memory fake is a unit-test seam, not a way to run the app
  without Azure.
- No CI pipeline definition, no reverse proxy, no production hosting: deployment stage 1 is local
  Docker only (PRD "Deployment (stage 1)").
- No frontend scaffold. The React app is a Figma Make export consumed at feature 10 (PRD
  Decisions #17/#22); nothing under `src/web/` is authored here.
- No shared-kernel libraries, no CQRS/mediator pipeline, no repository-of-repository abstractions
  beyond what a layer boundary requires.
