# TrailBlaze

A public-read **activity journal**. Anyone can browse activities ordered by date descending with
their cover images; signing in reveals the images and videos each activity carries.

TrailBlaze is one shared feed, not a set of private diaries — everyone posts, and ownership
decides who may *edit* an entry rather than who may read it.

## Start here

| Document | What it holds |
|---|---|
| [docs/PRD.md](docs/PRD.md) | The product definition — 30 logged decisions, the canonical data model, the permission matrix, and the API surface |
| [docs/features/00-mission-1-sprint.md](docs/features/00-mission-1-sprint.md) | The 11-feature ladder, dependency order, and the Definition of Done |
| [docs/testing-and-tdd.md](docs/testing-and-tdd.md) | Test tiers and the RED → GREEN → refactor discipline |
| [docs/tech-debt/00-debt-log.md](docs/tech-debt/00-debt-log.md) | Known divergences between the code and the standard, and who owns each |
| [docs/features/backlog.md](docs/features/backlog.md) | Ideas that are *not* yet features |

## Stack

| Layer | Choice |
|---|---|
| Backend | ASP.NET Core 10, layered `Api / Interface / Model / Repository / Service`, each with a sibling xUnit project |
| Database | **Azure SQL Database** via EF Core (`Microsoft.EntityFrameworkCore.SqlServer`; migrations applied by the deployment pipeline) |
| Media | Azure Blob Storage — a **public** container for cover images, a **private** one for activity media, reached via short-lived SAS URLs |
| Identity | Entra ID (bearer tokens; users auto-provisioned; one admin seeded) |
| Frontend | React 19 + Vite + TypeScript + Tailwind, exported from **Figma Make** |
| Hosting | API → **Azure Container Apps** (the `src/api/Dockerfile` image); web → **Azure Static Web Apps** by GitHub workflow |
| Local run | `dotnet run` from `src/api/TrailBlaze.Api`, settings from user-secrets |

## Working on it

The workflow lives in `.claude/` and runs on slash commands:

```
/capture <kind> NN    # a spec, a bug report, or a debt item
/next                 # pick the lowest-numbered feature and implement it TDD
/implement NN         # implement a specific feature
/add-test NN          # RED only — write the missing tests
/list-features        # every feature with its priority and status
/sprint-status        # progress, DoD, branches, open PRs
/archive NN           # after the PR merges
```

Backend tests run from `src/api/` with `dotnet test`. Two containers back the database and storage
tiers — start them with `docker compose -f docker-compose.test.yml up -d` first, or those 28 tests
**skip** rather than fail. See [testing-and-tdd.md](docs/testing-and-tdd.md).

## Running it locally

The API refuses to start without its required settings (`DbConnection`, `BlobConnection`,
`AllowedOrigins`), so there is no zero-configuration boot. Locally they come from user-secrets —
never from `appsettings.json` or `launchSettings.json`, which are version-controlled:

```bash
cd src/api/TrailBlaze.Api
dotnet user-secrets set "DbConnection" "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=TrailBlaze;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False"
dotnet user-secrets set "BlobConnection" "<azure storage connection string>"
dotnet run
```

There is no `docker-compose.yml`, and none is wanted: the API is deployed to **Azure Container
Apps** from the image `src/api/Dockerfile` builds, and the web app to **Azure Static Web Apps** by
its own workflow, and neither consumes a local multi-service stack. `src/api/docker-compose.test.yml`
is not a local stack — it starts **no application process**, only the two containers the test tier
talks to, and it is documented in [testing-and-tdd.md](docs/testing-and-tdd.md).

Three things to arrange before the first run, all of them outside the app:

- the database must **already exist** — the pipeline migrates the schema, and neither the API nor
  `dotnet ef` creates the database itself;
- the SQL Server's firewall must allow the address you connect from;
- the `covers`, `avatars` and `media` containers must exist in your Azure Storage account — the API
  reads and writes blobs but never provisions containers, so the first upload otherwise fails with
  `ContainerNotFound`.

**Schema changes are not applied by the API.** Migrations run in the deployment pipeline, before a
new revision takes traffic — Azure Container Apps runs several replicas, and replicas migrating
concurrently at startup race each other over the same DDL. Until that pipeline exists, apply them
by hand:

```bash
DbConnection="<the same value>" dotnet ef database update --project src/api/TrailBlaze.Repository
```

That factory reads `DbConnection` **from the environment only**, and refuses to run without it.
That is deliberate: with `dotnet ef` now the only thing that migrates, a default that silently
pointed somewhere else would migrate the wrong database and report success.

## Three things to know before you start

1. **The suite is real but shallow.** `dotnet test` from `src/api/` discovers 59 tests. With the two
   test containers running, all 59 pass; with nothing configured, **31 pass and 28 skip** — the
   skips are the database tier, and they are reported rather than hidden. What is covered is the
   foundation: the audit interceptor, the soft-delete filter executed against the engine, the
   **migration set actually applying**, the fluent bounds reaching the schema, storage routing
   including a minted SAS the server accepts, and the host's startup rules. The one product slice
   that has shipped — feature 02's profile routes, avatar upload and upload validator — is **not**
   covered: its tests were deliberately deferred, and
   [item 12](docs/tech-debt/12-feature-02-tests-deferred.md) tracks writing them. "Green" here means
   the foundation is green.
2. **`develop` is not deployable until feature 09 merges.** Features 04–08 build the CRUD and
   media mechanics while every signed-in user can still write anything; 09 imposes the ownership
   and admin rules. See the sequencing note in the sprint file.
3. **Azure Blob is real in every environment**, tests included — there is no `IStorageRepository`
   fake, so nothing stands in for the real implementation. The `Category=Container` storage tier runs
   the real `AzureBlobStorageRepository` against the **Azurite emulator by default**, which needs no
   credentials, and against a real account when `TRAILBLAZE_STORAGE_CONNECTION` names one. It skips
   when nothing answers.
