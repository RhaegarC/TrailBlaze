# TrailBlaze

A public-read **activity journal**. Anyone can browse activities ordered by date descending with
their cover images; signing in reveals the images and videos each activity carries.

TrailBlaze is one shared feed, not a set of private diaries — everyone posts, and ownership
decides who may *edit* an entry rather than who may read it.

## Start here

| Document | What it holds |
|---|---|
| [docs/PRD.md](docs/PRD.md) | The product definition — 25 logged decisions, the canonical data model, the permission matrix, and the API surface |
| [docs/features/00-mission-1-sprint.md](docs/features/00-mission-1-sprint.md) | The 11-feature ladder, dependency order, and the Definition of Done |
| [docs/testing-and-tdd.md](docs/testing-and-tdd.md) | Test tiers and the RED → GREEN → refactor discipline |
| [docs/features/backlog.md](docs/features/backlog.md) | Ideas that are *not* yet features |

## Stack

| Layer | Choice |
|---|---|
| Backend | ASP.NET Core 10, layered `Api / Interface / Model / Repository / Service`, each with a sibling xUnit project |
| Database | **Azure SQL Server** via EF Core (`Microsoft.EntityFrameworkCore.SqlServer`; migrations applied at startup) |
| Media | Azure Blob Storage — a **public** container for cover images, a **private** one for activity media, reached via short-lived SAS URLs |
| Identity | Entra ID (bearer tokens; users auto-provisioned; one admin seeded) |
| Frontend | React 19 + Vite + TypeScript + Tailwind, exported from **Figma Make** |
| Local run | Docker Compose (the API; the database is a real Azure SQL Database) |

## Working on it

The workflow lives in `.claude/` and runs on slash commands:

```
/capture feature NN   # stress-test an idea into a spec
/next                 # pick the lowest-numbered feature and implement it TDD
/implement NN         # implement a specific feature
/add-test NN          # RED only — write the missing tests
/sprint-status        # progress, DoD, branches, open PRs
/archive NN           # after the PR merges
```

Backend tests run from `src/api/` with `dotnet test`.

## Running it locally

The API refuses to start without its required settings (`DbConnection`, `BlobConnection`,
`AllowedOrigins`), so there is no zero-configuration boot. Docker Compose supplies all three:

```bash
cp .env.example .env    # then fill it in
docker compose up --build
```

Compose brings up the API and nothing else. There is **no database container**: Azure SQL Database
is managed and has no image, so `DbConnection` points at a real Azure SQL Database and the API
reaches it over the network. Two consequences worth knowing before the first run —

- the database must already exist (the API migrates the schema at startup, it does not create the
  database), and
- the SQL Server's firewall must allow the address you are connecting from.

Migrations are applied at startup, so a fresh database is brought up to schema by simply starting
the API.

One thing Compose does **not** do is create the `covers`, `avatars` and `media` containers in your
Azure Storage account. The API reads and writes blobs but never provisions containers — that is a
deployment concern, out of band from the app. Create them once, or the first upload fails with
`ContainerNotFound`.

To run the API outside Docker instead, put the same values in user-secrets — never in
`appsettings.json` or `launchSettings.json`, which are version-controlled:

```bash
cd src/api/TrailBlaze.Api
dotnet user-secrets set "DbConnection" "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=TrailBlaze;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False"
dotnet user-secrets set "BlobConnection" "<azure storage connection string>"
```

## Three things to know before you start

1. **The suite is real but shallow.** `dotnet test` from `src/api/` runs 31 tests across the three
   tiers, 30 of them by default — the storage-integration test skips without
   `TRAILBLAZE_STORAGE_CONNECTION`, and only it exercises the real Azure implementation. What is
   covered today is the foundation: the audit interceptor, the soft-delete filter, the model's
   agreement with its migration snapshot, storage routing at unit level, and the host's startup
   rules. No product behaviour is covered, because none exists yet.
2. **`develop` is not deployable until feature 09 merges.** Features 04–08 build the CRUD and
   media mechanics while every signed-in user can still write anything; 09 imposes the ownership
   and admin rules. See the sequencing note in the sprint file.
3. **Azure Blob is real in every environment**, tests included. Unit tests inject an in-memory
   `IStorageService` fake so the RED → GREEN loop stays offline; a tagged
   `Category=StorageIntegration` tier exercises the real account and needs credentials.
