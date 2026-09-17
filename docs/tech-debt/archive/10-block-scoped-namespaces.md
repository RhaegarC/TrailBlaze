# 10 — Namespaces were block-scoped

Status: **Archived** — resolved 2026-09-16 by a mechanical PR, before this register existed · [00-debt-log.md](../00-debt-log.md)
Source: STANDARD §12.10 · Opened: 2026-09-15 · Archived: 2026-09-17

## What it was

STANDARD §1 prescribed **file-scoped namespaces** with `using` directives beneath the namespace.
Every authored file used the block-scoped form instead, split between two sub-styles — `using` above
the namespace in some files, inside it in others — so the solution had three shapes and the standard
matched none of them.

The divergence widened rather than narrowed during feature 01: new files followed the code they
landed beside, as new files do, which is the mechanism that turns a style difference into a
permanent one.

## How it resolved

The mechanical PR was made on 2026-09-16. All **36 authored files** are file-scoped with `using`
directives beneath the namespace, and §1 records the two files that cannot be:

- `Program.cs`, which has no namespace at all — top-level statements must precede one, and `using`
  directives must precede the statements, so the file cannot take this form;
- `Migrations/`, which `dotnet ef` regenerates in the generator's style, where reformatting buys
  nothing the next `migrations add` does not undo. §1's rule is therefore read as applying to code
  this solution authors.

## Lesson

**This is the register's clearest example of a divergence that stayed open because it was deferred as
mechanical.** Nothing about the fix was hard; the whole cost was that it touched many files, and a
change touching many files looks large in review while doing nothing interesting. So it waited — and
feature 01, arriving in the meantime, copied the existing style and made the divergence worse.

The general form is worth carrying: **a mechanical fix that everyone agrees with is at its cheapest
the moment it is agreed to.** Deferring it not only postpones the cost but authorises new code to
join the wrong side, so the eventual fix is larger than the one that was deferred. §1's "Reconciled
2026-09-16" note records this in the standard itself, and this file is the longer version.

## Why archived rather than deleted

Because of the lesson, which is the reason §12.10 was kept in the standard rather than struck out
when it closed. It is the founding example for this register's `Kind: process` category: the item was
not a defect in the code but in how a known, agreed fix was allowed to sit.
