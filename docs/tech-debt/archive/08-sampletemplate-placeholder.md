# 08 — `SampleTemplate/placeholder.txt`

Status: **Archived** — resolved by removal; no PR, no code change · [00-debt-log.md](../00-debt-log.md)
Source: STANDARD §12.8 · Opened: 2026-09-15 · Archived: 2026-09-17

## What it was

A `SampleTemplate/placeholder.txt` file left in the repository by the upstream template this
solution was generated from. It held no content and was referenced by nothing in the code.

It mattered slightly more than a stray file usually does, because STANDARD §11 described a CI job
whose purpose was to pack that template — so §11 referred to a directory that did not need to exist
here, which made §11 read as partly-satisfied rather than entirely unwritten.

## How it resolved

The file and the template it belonged to do not live here any more. Nothing referenced either, so
the resolution was a deletion rather than a change.

§11's template job survives that deletion as prose describing a job that cannot run in this
repository. Item [11](../11-no-ci-pipeline.md) owns deciding whether to build the other job or to
remove the description.

## Lesson

**A scaffold's leftovers are not neutral: they make unrelated documentation look satisfiable.** §11's
template job was not a coincidence — it was written for the repository the scaffold came from, and
the presence of `SampleTemplate/` kept the description plausible long after this repository had
stopped being that repository. The file was trivial; the confusion it licensed was not.

## Why archived rather than deleted

It is the register's only example of an item whose resolution was *deletion with no diff*, and the
mechanism that closed it — the template not being here — is the same fact that makes §11's second job
out of scope. Recorded so that fact has a home when 11 is worked.
