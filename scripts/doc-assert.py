#!/usr/bin/env python3
"""Assert the invariants the documentation indexes depend on.

Every check below exists because the defect it catches has already happened here at least
once. Run from the repository root: `python3 scripts/doc-assert.py`.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FEATURES = ROOT / "docs" / "features"
DEBT = ROOT / "docs" / "tech-debt"
SPRINT = FEATURES / "00-mission-1-sprint.md"
REGISTER = DEBT / "00-debt-log.md"
TEST_STRATEGY = ROOT / "docs" / "testing-and-tdd.md"
COMMANDS = ROOT / ".claude" / "commands"
README = ROOT / "README.md"
STANDARD = ROOT / "src" / "api" / "STANDARD.md"
REPOSITORY = ROOT / "src" / "api" / "TrailBlaze.Repository"
SKIP_DIRS = {".git", "node_modules", "bin", "obj", ".gitnexus", ".vs"}
# Frozen records: they describe a state on a date and are never brought forward.
ARCHIVED_DIRS = {FEATURES / "archive", DEBT / "archive"}


def markdown_files():
    for path in sorted(ROOT.rglob("*.md")):
        if any(part in SKIP_DIRS for part in path.relative_to(ROOT).parts):
            continue
        yield path


def is_archived(path):
    return path.parent in ARCHIVED_DIRS


def numbered(directory, archived):
    """`docs/{features,tech-debt}/NN-name.md` as {number: path}, excluding `00`."""
    folder = directory / "archive" if archived else directory
    found = {}
    for path in sorted(folder.glob("*.md")) if folder.is_dir() else []:
        match = re.match(r"(\d\d)-", path.name)
        if match and match.group(1) != "00":
            found[match.group(1)] = path
    return found


def prose(text):
    """The document's prose, with fenced code blocks removed and offsets preserved."""
    out = []
    fenced = False
    for line in text.splitlines(keepends=True):
        if line.lstrip().startswith("```"):
            fenced = not fenced
            out.append("\n" if line.endswith("\n") else "")
        else:
            out.append("" if fenced else line)
    return "".join(out)


def relative(path, target):
    return str(target.relative_to(ROOT))


# --- Checks -------------------------------------------------------------------


def links_resolve():
    """Every relative markdown link points at a file that exists."""
    failures = []
    for path in markdown_files():
        text = prose(path.read_text(encoding="utf-8"))
        for target in re.findall(r"\]\(([^)\s]+)\)", text):
            if target.startswith(("http://", "https://", "mailto:", "#")):
                continue
            target = target.split("#", 1)[0]
            if not target or target.startswith("/"):
                continue
            if not (path.parent / target).resolve().exists():
                failures.append(f"{relative(ROOT, path)} -> {target}")
    return failures


def counts_have_one_home():
    """A live test count appears only in the test strategy, which owns it.

    A count on a dated line is a measurement of a moment rather than a claim about now,
    and the strategy doc's own drift record needs to be able to quote one. Debt items are
    exempt for the same reason: recording what a run reported, on what date, is the item's
    function — a register entry is evidence, not a second home.
    """
    pattern = re.compile(r"\b\d+ (?:tests?\b|passed\b|skipped\b)|\bTotal: \d+")
    dated = re.compile(r"\b(?:19|20)\d\d-\d\d-\d\d\b|\bas of\b|\bMeasured\b|\bChecked\b")
    failures = []
    for path in markdown_files():
        if path == TEST_STRATEGY or is_archived(path) or DEBT in path.parents:
            continue
        text = prose(path.read_text(encoding="utf-8"))
        for number, line in enumerate(text.splitlines(), start=1):
            if pattern.search(line) and not dated.search(line):
                failures.append(f"{relative(ROOT, path)}:{number}: {line.strip()}")
    return failures


def ci_is_not_contradicted():
    """No document may require a CI job to be green while there is no CI workflow."""
    if (ROOT / ".github" / "workflows").is_dir():
        return []
    requirement = re.compile(
        r"CI'?s? (?:template )?job (?:stays|remains|is) green|\bCI runs? on every\b",
        re.IGNORECASE,
    )
    failures = []
    for path in markdown_files():
        # §11 states the target and its own note records that it is not built; discussing
        # the gap is not the defect. Requiring the job to pass is.
        for number, line in enumerate(prose(path.read_text(encoding="utf-8")).splitlines(), 1):
            if requirement.search(line):
                failures.append(f"{relative(ROOT, path)}:{number}: {line.strip()}")
    return failures


def sprint_table_covers_features():
    """Every feature file has exactly one sprint-table row, archived ones marked so."""
    text = prose(SPRINT.read_text(encoding="utf-8"))
    rows = re.findall(r"^\|\s*(\d\d)\s*\|(.+)$", text, re.MULTILINE)
    linked = {}
    for number, row in rows:
        for target in re.findall(r"\]\(([^)\s]+)\)", row):
            linked.setdefault(target.split("#")[0], []).append(number)

    failures = []
    for archived in (False, True):
        for number, path in numbered(FEATURES, archived).items():
            expected = f"archive/{path.name}" if archived else path.name
            if expected not in linked:
                failures.append(f"no sprint-table row links to {relative(ROOT, path)}")
    for number, path in numbered(FEATURES, True).items():
        expected = f"archive/{path.name}"
        row = next(
            (
                line
                for line in text.splitlines()
                if expected in line and line.lstrip().startswith("|")
            ),
            "",
        )
        if row and "archiv" not in row.lower():
            failures.append(f"{relative(ROOT, path)}: sprint-table row does not say archived")
    for target in linked:
        if re.match(r"\d\d-", Path(target).name) and not (SPRINT.parent / target).exists():
            failures.append(f"sprint table links to missing {target}")
    return failures


def debt_register_is_complete():
    """Every debt item has a register row, and every register row has an item."""
    if not REGISTER.exists():
        return [f"missing {relative(ROOT, REGISTER)}"]
    text = prose(REGISTER.read_text(encoding="utf-8"))
    failures = []
    for archived in (False, True):
        for number, path in numbered(DEBT, archived).items():
            prefix = "archive/" if archived else ""
            if f"{prefix}{path.name}" not in text:
                failures.append(f"no register row names {relative(ROOT, path)}")
    for target in re.findall(r"\]\(([^)\s]+\.md)\)", text):
        if not re.match(r"\d\d-", Path(target).name):
            continue
        if not (REGISTER.parent / target).exists():
            failures.append(f"register links to missing {target}")
    return failures


def status_is_a_lifecycle_marker():
    """A document's `Status:` line marks its lifecycle, and matches where it lives."""
    failures = []
    for folder, marker in ((FEATURES, "feature"), (DEBT, "item")):
        for archived in (False, True):
            for number, path in numbered(folder, archived).items():
                line = next(
                    (
                        l
                        for l in path.read_text(encoding="utf-8").splitlines()[:6]
                        if l.startswith("Status:")
                    ),
                    None,
                )
                where = relative(ROOT, path)
                if line is None:
                    failures.append(f"{where}: no Status: line")
                    continue
                said = ("archived" in line.lower())
                if said != archived:
                    failures.append(
                        f"{where}: Status line says {'archived' if said else 'not archived'}"
                        f" but the file is {'in' if archived else 'not in'} archive/"
                    )
                if not archived and re.search(r"\bdone\b|\bmerged\b", line, re.I):
                    failures.append(f"{where}: {marker} Status line states more than its lifecycle")
    return failures


def no_status_outside_the_sprint_table():
    """Nothing but the sprint table and archived records states a feature's status.

    `.claude/` is a workflow, not an index: its files describe the archiving *procedure*,
    and the documented form of a Status line, so a status word there is instruction.
    A document's own `Status:` line is its lifecycle marker — check 6 owns that.
    """
    pattern = re.compile(r"\*\*(?:Not started|In progress|Done|Archived|done|archived)\*\*")
    failures = []
    for path in markdown_files():
        if path == SPRINT or is_archived(path) or ".claude" in path.parts or DEBT in path.parents:
            continue
        for number, line in enumerate(prose(path.read_text(encoding="utf-8")).splitlines(), 1):
            if number <= 6 and line.startswith("Status:"):
                continue
            if pattern.search(line):
                failures.append(f"{relative(ROOT, path)}:{number}: {line.strip()}")
    return failures


def readme_lists_every_command():
    """The README's command block names every slash command, and invents none."""
    if not README.exists():
        return [f"missing {relative(ROOT, README)}"]
    documented = set(re.findall(r"^/([a-z-]+)", README.read_text(encoding="utf-8"), re.MULTILINE))
    defined = {path.stem for path in COMMANDS.glob("*.md")}
    return [f"/{name} is defined in .claude/commands but not listed in the README"
            for name in sorted(defined - documented)] + [
        f"/{name} is listed in the README but has no .claude/commands/{name}.md"
        for name in sorted(documented - defined)
    ]


def no_relationship_the_model_does_not_declare():
    """A feature spec may not assert a foreign key or cascade the model does not declare.

    Only the specs are checked. The PRD's own `FK ->` labels are product content and the same
    debt, filed as `docs/tech-debt/23-foreign-keys-asserted-that-do-not-exist.md`, which owns
    correcting them with features 04 and 06. The reason the specs cannot wait is that they tell
    an implementer what to build: a relationship they name but the schema does not have would be
    silently absent — orphaned rows, unreclaimed blobs, no failing test. The check lifts itself
    the moment the model declares a relationship.
    """
    if any(
        re.search(r"HasForeignKey|HasOne\b|HasMany\b|OnDelete|DeleteBehavior", path.read_text(encoding="utf-8"))
        for path in REPOSITORY.rglob("*.cs")
    ):
        return []
    pattern = re.compile(r"FK\s*(?:→|->)|\(FK cascade\)|\bFK\b(?=[^.]{0,60}\bcascade)")
    failures = []
    for number, path in numbered(FEATURES, False).items():
        text = prose(path.read_text(encoding="utf-8"))
        for line_number, line in enumerate(text.splitlines(), start=1):
            if pattern.search(line):
                failures.append(f"{relative(ROOT, path)}:{line_number}: {line.strip()}")
    return failures


CHECKS = [
    ("Every relative markdown link resolves", links_resolve),
    ("Test counts live only in the test strategy", counts_have_one_home),
    ("No document asserts CI that does not exist", ci_is_not_contradicted),
    ("The sprint table covers the feature set", sprint_table_covers_features),
    ("The debt register is complete and points at real files", debt_register_is_complete),
    ("Status lines are lifecycle markers matching their location", status_is_a_lifecycle_marker),
    ("No document states a feature's status outside the sprint table",
     no_status_outside_the_sprint_table),
    ("No spec asserts a relationship the model does not declare",
     no_relationship_the_model_does_not_declare),
    ("The README lists every slash command", readme_lists_every_command),
]


def main():
    failed = False
    for title, check in CHECKS:
        failures = check()
        if failures:
            failed = True
            print(f"FAIL  {title}")
            for failure in failures:
                print(f"        {failure}")
        else:
            print(f"ok    {title}")
    print()
    print("documentation is consistent" if not failed else "documentation has drifted")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
