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
| Database | SQL Server via EF Core (Testcontainers `mssql` for integration tests) |
| Media | Azure Blob Storage — a **public** container for cover images, a **private** one for activity media, reached via short-lived SAS URLs |
| Identity | Entra ID (bearer tokens; users auto-provisioned; one admin seeded) |
| Frontend | React 19 + Vite + TypeScript + Tailwind, exported from **Figma Make** |
| Local run | Docker Compose (API + SQL Server) |

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

## Two things to know before you start

1. **`develop` is not deployable until feature 09 merges.** Features 04–08 build the CRUD and
   media mechanics while every signed-in user can still write anything; 09 imposes the ownership
   and admin rules. See the sequencing note in the sprint file.
2. **Azure Blob is real in every environment**, tests included. Unit tests inject an in-memory
   `IStorageService` fake so the RED → GREEN loop stays offline; a tagged
   `Category=StorageIntegration` tier exercises the real account and needs credentials.
