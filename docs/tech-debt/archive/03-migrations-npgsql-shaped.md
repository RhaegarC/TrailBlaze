# 03 — Migrations were Npgsql-shaped

Status: **Archived** — resolved by [feature 01](../features/archive/01-foundation.md), merged to `develop` in PR #3 · [00-debt-log.md](../00-debt-log.md)
Source: STANDARD §12.3 · Opened: before 2026-09-15 · Archived: 2026-09-17

## What it was

The solution was generated from a generic layered .NET scaffold whose migrations were written for
**Npgsql** (PostgreSQL). `TrailBlaze.Repository/Migrations` and
`TrailBlazeContextModelSnapshot` therefore described the wrong provider — including `nvarchar(max)`
mappings on the audit snapshots that PostgreSQL would not accept — while the intended database was
Azure SQL Database.

## How it resolved

Feature 01 replaced `Npgsql` with `Microsoft.EntityFrameworkCore.SqlServer` and regenerated the
migration set, so `Migrations/` is SQL Server-shaped and the snapshot agrees with the model. The
model-versus-snapshot agreement is now asserted by a test, which is what stops the divergence
returning silently.

**The strategy question the item raised was settled at the same time, and that is the part worth
keeping.** An `IHostedService` that applied migrations at startup was built and then removed: Azure
Container Apps runs several replicas, and concurrent startup migrations race each other over the same
DDL. Migrations are instead applied by the **deployment pipeline** with `dotnet ef database update`,
before a new revision takes traffic. Supporting that, `TrailBlazeContextFactory` resolves
`DbConnection` from the environment and throws when absent, rather than defaulting to a local string
that would migrate the wrong database and report success.

## Lesson

A scaffold's migrations are provider-specific in a way that is invisible until something tries to run
them, and the fix is a regeneration rather than a patch. The removal of the startup migrator is the
sharper half: **"it works on one instance" was never the question** — the question was how many
replicas run, and the answer invalidated the design.

## Why archived rather than deleted

The provider swap is complete and covered. This file remains because the migration strategy is a live
rule with a documented reason, and the reason is easier to honour when the failure it came from is
written down. Item [11](../11-no-ci-pipeline.md) notes the other half: with no pipeline yet, applying
a migration is currently a manual step.
