# 18 — `.claude/settings.json` grants another repository's permissions

Status: **open** · Kind: hygiene · Impact: friction · Area: Workflow
Source: found 2026-09-17 (not a §12 item) · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

`.claude/settings.json` is version-controlled and read by every session in this repository. **30 of
its 75 allow entries belong to a different project** (`/d/TecShare/AITraining/repo`, an FMS
solution on GitLab):

| Kind of entry | Examples |
|---|---|
| Commands against an absolute path outside this repo | `Bash(git -C /d/TecShare/AITraining/repo log --oneline -3)`, `… show b9453b2`, `… checkout develop` |
| Package names from another workspace | `Bash(pnpm --filter @fms/auth test)`, `@fms/admin`, `@fms/user` |
| Another forge's CLI | `Bash(glab mr *)`, `Bash(glab auth *)` — this project uses GitHub |
| Another project's feature names | `git commit -m '… feature 06-space-form-crud …'`, `… feature 07-submission-apis …`, `… #03-entra-auth …` |
| A YAML validator pinned to another repo | the `python -c "import yaml … /d/TecShare/AITraining/repo/.gitlab-ci.yml"` entries |

They were committed deliberately — the user was asked, and chose "include all of it" rather than
splitting the file. This item is the follow-up, not a reversal.

## Why it matters

**A permission file is a standing grant, and these grant access to a repository this one does not
own.** A session working in TrailBlaze can, without prompting, run `git commit`, `git push`, and
`git checkout` in `/d/TecShare/AITraining/repo`, plus arbitrary `glab` subcommands. Nothing in
TrailBlaze needs any of that. The grant is not for a one-off action that already happened; it is
re-issued to every future session.

**It also makes the file unreadable as a record.** The file's legitimate job is to answer "what may a
session in this repo do?" — and a third of the answer is about a different codebase. Reviewing a
change to it means reading past two dozen entries that can never matter here, which is how a
permission entry that *does* matter slips through.

**One entry is broader than the rest and deserves its own decision.** `Bash(cat > *)` permits
overwriting any file by redirect, with no path constraint. It was added to make one write convenient
and it is not scoped the way the neighbouring `Read(//tmp/**)` and `additionalDirectories` entries
are.

## Evidence

Checked against the file 2026-09-17.

- `json` parse: 75 entries in `permissions.allow`; 30 match `TecShare/AITraining`, `@fms`, or a
  `glab` prefix.
- The entries are `.claude/settings.json:4-76`, with the `AITraining` block concentrated at lines
  47–70.
- Committed in PR #6 under an explicit choice to include the whole set.
- `git status` shows the file dirty with a session-added `Bash(gh api *)` entry — noted because it
  means the file re-dirties on every new permission, so any fix must not be surprised by it.

## Testability

**doc-assertion**, and this is one of the clearest cases in the register for it.

The invariant is a durable property of a version-controlled file: no allow entry may name a path
outside this repository. A test that reads `.claude/settings.json`, parses the allow list, and fails
on an entry containing `TecShare/AITraining` or an `@fms` package name is a real assertion with a
real failure mode — the day someone pastes another repo's permission back in.

Confirm it goes red by re-adding one entry. It should also be written to *not* fail on the merely
broad `Bash(cat > *)`, which is a separate judgement recorded above rather than a path violation.

## Repair plan

1. **Remove the 30 cross-repository entries**, leaving the ones this project uses.
2. **Decide `Bash(cat > *)` separately.** Either delete it, or scope it to a path the way
   `Read(//tmp/**)` and the `additionalDirectories` list are scoped. Do not fold this into the
   cleanup silently — it is a different question with a different answer.
3. **Write the `doc-assertion` test** described above under `TrailBlaze.Api.Test`, or wherever this
   solution keeps assertions about repository-level files. It is the part that stops the file
   drifting back.
4. **Let the cross-session friction settle first.** The file is dirty with an entry the harness
   appended, and removing entries while a session is appending them risks re-adding. Run this on its
   own branch, after the current work has merged.

## Out of scope / related

- **The other repository's `.claude/settings.json` is not this item's business.** If it carries
  TrailBlaze's entries, that is a mirrored problem for that repository to file.
- **`\tmp` in `additionalDirectories`** is a Windows path with a leading backslash — almost certainly
  meant to be `/tmp`, which is what the `Read(//tmp/**)` entry beside it uses. Worth confirming, but
  it is a one-line fix rather than a reason to hold this item open.
- **This item is self-referential.** The agent fixing it runs under the permissions it is removing,
  which is a further reason to do it last and alone.
- **The `.gitignore` question** — whether `.claude/settings.json` should be version-controlled at
  all — is deliberately not raised here. Team-wide permissions are worth sharing; the defect is that
  this file's set includes another project's, not that the file exists.

## Close checklist

- [ ] No allow entry names a path, package or feature outside this repository
- [ ] `Bash(cat > *)` was decided on its own terms, and the decision recorded
- [ ] A `doc-assertion` test fails when a foreign entry is added back
- [ ] The file was cleaned on its own branch, and no session-added entry was lost in the process
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](../00-debt-log.md)
