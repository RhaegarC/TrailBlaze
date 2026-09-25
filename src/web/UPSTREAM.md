# The integrated app, and the export it is ported from

`src/web/` is the application. [`figma/`](../../figma/) at the repository root holds the Figma Make
export exactly as Figma produced it: it is never installed, never built and never deployed. A UI
change is made in Figma Make, re-exported into `figma/`, and ported here by hand.

The port is the whole of this file's subject, because the failure it guards against is silent. An
export re-applied carelessly does not break the build — it replaces the API wiring with mock data,
the app still runs, and the screens look correct.

## What is upstream-owned, and what is not

The rule is derived rather than listed, so it cannot go stale: **a path is upstream-owned if the
same path exists under `figma/`.** Everything else under `src/web/` — the API client, the auth
wiring, this file — belongs to the integration, and an export never touches it.

So a re-export may replace every file it ships, and may not delete anything it does not ship.

Baseline: 65b6e17

The baseline is the `figma/` import that `src/web/` currently reflects. It is recorded here rather
than inferred from history because an import may be skipped, and "the previous commit" would then
be the wrong common ancestor — the merge would still succeed, against the wrong version.

## Porting a new export

```bash
# 1. Replace figma/ wholesale with the new export, keeping nothing of the old.
git add -A figma/ && git commit -m "chore(web): Import the Figma export of <date>"

# 2. The worklist: what upstream added, removed and changed, and the merge for each.
python3 scripts/web-seam.py --plan

# 3. Port it. A changed file is merged against the baseline, never copied over.

# 4. Record the new baseline in this file — the commit from step 1 — and run the check.
python3 scripts/web-seam.py
```

A changed file is ported as a three-way merge:

```bash
git show <baseline>:figma/src/App.tsx > /tmp/base.tsx
git merge-file -p src/web/src/App.tsx /tmp/base.tsx figma/src/App.tsx > /tmp/merged.tsx
mv /tmp/merged.tsx src/web/src/App.tsx
```

Merging rather than copying is what makes the integration survive a re-export: the merge takes
upstream's new screens and keeps the edits made here, and reports a conflict exactly where the two
disagree. A copy takes upstream's version and loses the wiring, without saying so.

## The seam

Every file whose content differs from its upstream copy is marked, so the difference has a stated
place to live and the check can tell an intended edit from a drifted one:

```tsx
// @integration:begin <what this region supplies>
…
// @integration:end
```

Inside JSX children the same markers are written as `{/* … */}`. The check reads them as text
rather than parsing the file, so both spellings are the same thing to it.

**The rule the check enforces: every line that differs from upstream falls inside a marked region
of `src/web/`.** Only the integrated file can carry the mark — the export is pristine by definition,
so its side is never marked.

A region is removed by **leaving its markers and putting nothing between them**, not by deleting
the lines outright. A bare deletion leaves no region to point at and is reported as drift. That is
deliberate: the marker pair is the durable record that upstream ships something here and this app
supplies its own, and it is what makes the next re-export conflict where it should rather than
quietly taking the mock data back.

Keep the seam small. The export's screens already take their data as props, so integration is a
question of where the data comes from rather than of editing screens: prefer a new file under
`src/web/src/` over an edit to `App.tsx`, and prefer one call-site edit over a rewritten function.
A seam of a few dozen lines merges cleanly every time; a seam spread across every screen makes each
re-export a manual reconciliation.

## What the check cannot see

It reads files, so it cannot see a seam that compiles and is wrong — a hook wired to the wrong
endpoint, or a screen still reading a mock that was moved rather than deleted. Feature 10 is
verified end to end, and that pass is what covers those.

**One clobber it cannot see, named rather than left looking covered.** Copying an export file over
one that was already integrated takes the markers away with the wiring, and a file with no markers
and no differences is indistinguishable from one that was never integrated at all. The check that
closes this is a marker requirement on `src/web/src/App.tsx` — non-empty once the wiring lands. It
is added when the wiring is, not before, because until then it would fail on a correct tree.
