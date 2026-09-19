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
| Identity | Entra ID (bearer tokens; users auto-provisioned on first sign-in; one admin, set by hand in the database) |
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
tiers — start them with `docker compose -f docker-compose.test.yml up -d` first, or those tests
**skip** rather than fail. See [testing-and-tdd.md](docs/testing-and-tdd.md), which is also the only
place the test counts are written down.

## Running it locally

The API refuses to start without its required settings (`DbConnection`, `BlobConnection`,
`AllowedOrigins`), so there is no zero-configuration boot. Locally they come from user-secrets —
never from `appsettings.json` or `launchSettings.json`, which are version-controlled:

```bash
cd src/api/TrailBlaze.Api
dotnet user-secrets set "DbConnection" "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=TrailBlaze;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False"
dotnet user-secrets set "BlobConnection" "<azure storage account connection string>"
dotnet run
```

**The administrator is a database row, not a setting.** Every user the app provisions lands on
`Role = User`; to make one an admin, sign in and hit `GET /user/me` once so the row exists (the
app provisions it on first sight of your object id), then update it directly:

```sql
UPDATE Users SET Role = 'Admin' WHERE Id = '<the Entra object id>';
```

Nothing in the application seeds, promotes or writes that column. A deployment that never runs this
statement starts and looks perfectly healthy with no administrator — the failure surfaces the first
time an admin action is attempted, to the person who owns the credential, rather than as an
application that rewrites a privilege column on its own at boot.
[03-admin-seeding.md](docs/features/archive/03-admin-seeding.md#decisions) records why the startup seeder
that used to do this was removed.

**Or run against a local SQL Edge container** and skip the firewall rule entirely:

```bash
docker run -d --name tb-azure-sql-edge \
  -e ACCEPT_EULA=1 -e MSSQL_SA_PASSWORD=<a strong password> \
  -p 127.0.0.1:1433:1433 \
  -v <a host directory>:/var/opt/mssql/data \
  --shm-size 1g \
  mcr.microsoft.com/azure-sql-edge

cd src/api
DbConnection="Server=127.0.0.1,1433;Database=TrailBlaze;User Id=sa;Password=<the same password>;Encrypt=True;TrustServerCertificate=True" \
  dotnet ef database update --project TrailBlaze.Repository

cd TrailBlaze.Api
dotnet user-secrets set "DbConnection" "Server=127.0.0.1,1433;Database=TrailBlaze;User Id=sa;Password=<the same password>;Encrypt=True;TrustServerCertificate=True"
```

Note the differences from the Azure string, and that each is deliberate: the host is `127.0.0.1`
rather than a real server, and it carries `TrustServerCertificate=True` because SQL Edge serves a
self-signed certificate. Both are part of a **loopback-only exception** — [STANDARD.md](src/api/STANDARD.md)
§6 states it, and states that the keyword must never reach a string that names a real server. The
`-v` mount is what makes the database survive a container recreate; without it, `docker rm` takes
the schema with it. `--shm-size 1g` is not optional either: the engine fails opaquely on the 64 MB
default.

The container is separate from the test tier's. `src/api/docker-compose.test.yml` starts its own
SQL Edge for `dotnet test`, and both bind `127.0.0.1:1433`, so only one can run at a time. While
the dev container holds the port, a test run reaches *it*; that is safe — the tier creates and
drops a database per collection and never touches `TrailBlaze` — but stop the dev container to get
the isolated stack back.

There is no `docker-compose.yml`, and none is wanted: the API is deployed to **Azure Container
Apps** from the image `src/api/Dockerfile` builds, and the web app to **Azure Static Web Apps** by
its own workflow, and neither consumes a local multi-service stack. `src/api/docker-compose.test.yml`
is not a local stack — it starts **no application process**, only the two containers the test tier
talks to, and it is documented in [testing-and-tdd.md](docs/testing-and-tdd.md). The command above
is a `docker run` rather than a compose service for the same reason: nothing in the deployment
reads it, so there is no stack for compose to describe.

Against a real server, three things to arrange before the first run, all of them outside the app:

- the database must **already exist** — this is what the pipeline does rather than an engine
  limit, and the distinction matters: `dotnet ef database update` *does* create the database when
  it is absent, which is why the container instructions above need no `CREATE DATABASE` step. A
  deployed environment's database is provisioned deliberately, not as a side effect of a
  migration;
- the SQL Server's firewall must allow the address you connect from;
- the `covers`, `avatars` and `media` containers must exist in your Azure Storage account — the API
  reads and writes blobs but never provisions containers, so the first upload otherwise fails with
  `ContainerNotFound`.

Against the local container the first two do not apply: there is no firewall rule to add, and the
command above creates the database. The third still does — a local database does not stand in for
storage, and `BlobConnection` is required at startup either way, so point it at a real account or
at an Azurite container of your own. Nothing in this repository starts one for the application;
the Azurite in `docker-compose.test.yml` belongs to the test tier, which is why it has no volume
and drops the blobs it writes.

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

1. **The suite is real but shallow.** `dotnet test` from `src/api/` is green either way: with the two
   test containers running everything passes, and with nothing configured the container-backed tests
   **skip** rather than fail — they are reported rather than hidden. The counts live in
   [testing-and-tdd.md](docs/testing-and-tdd.md#the-container-tier), which is their only home, because
   a number copied into five documents is a number that goes stale in five places. What is
   covered is the foundation: the audit interceptor, the soft-delete filter executed against the
   engine, the **migration set actually applying**, the fluent bounds reaching the schema, storage
   routing including a minted SAS the server accepts, and the host's startup rules. Feature 03 added
   the first product coverage — the `Role` constraint and its backfill against a real engine, and the
   role-resolution logic — so the store-backed *service* tier now exists. Feature 02's
   slice — the profile routes, avatar upload and upload validator — is still **not** covered: its
   tests were deliberately deferred, and
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
