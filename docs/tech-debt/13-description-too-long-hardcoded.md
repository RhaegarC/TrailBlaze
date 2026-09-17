# 13 — `DescriptionTooLong` hardcodes `500`, against the convention its own file states

Status: **open** · Kind: hygiene · Impact: friction · Area: Model
Source: found 2026-09-17 (not a §12 item) · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

[Constant.cs:26-27](../../src/api/TrailBlaze.Model/Constant.cs#L26-L27) writes the limit as a literal:

```csharp
public const string DescriptionTooLong =
    "Description must be 500 characters or fewer.";
```

Its immediate sibling is composed from the constant it describes
([Constant.cs:32-33](../../src/api/TrailBlaze.Model/Constant.cs#L32-L33)):

```csharp
public static readonly string DisplayNameTooLong =
    $"Display name must be {UserProfile.DisplayNameLength} characters or fewer.";
```

The convention is not incidental — it is stated in a comment three lines above the offender
([Constant.cs:29-31](../../src/api/TrailBlaze.Model/Constant.cs#L29-L31)):

> Composed from the allowlists rather than written out beside them, so a value added to one cannot
> leave a message naming the old set. Correct prose is exactly the kind of thing that goes stale
> unnoticed.

`UserProfile.DescriptionLength` is `500` ([Constant.cs:177](../../src/api/TrailBlaze.Model/Constant.cs#L177)).
The two agree today.

## Why it matters

**The file explains why this is wrong, in the same file.** This is not a style preference imposed
from outside; the author of the surrounding code wrote the rule and then broke it one line up. That
makes it the clearest available example of the drift the convention exists to prevent — and the
cheapest to fix.

The failure mode is specific and silent. Changing `UserProfile.DescriptionLength` to 1000 updates the
validator and leaves the error message telling the user "500 characters or fewer". The user gets a
rejection that misstates the limit, and the mismatch surfaces as a support question rather than a
failing test. Nothing in the suite asserts the two agree.

**Cost is one line.** This is the smallest item in the register, which is why it is a reasonable
first item to run through `debt-fix` and prove the flow works before trusting it on item 02.

## Evidence

Checked against the code 2026-09-17.

- The literal is at line 27; `UserProfile.DescriptionLength = 500` at line 177; the convention
  comment at lines 29–31.
- `const` is the mechanical reason it was written this way: an interpolated string cannot be `const`,
  so making it consistent means `const` → `static readonly`. The four siblings that already compose
  are all `static readonly`. This is a deliberate and visible change of kind, not an oversight in
  the fix.
- Grepped the rest of `Constant.Message` 2026-09-17: no other message hardcodes a number that a
  nearby constant also declares. This is the only instance.

## Testability

**testable**, and cleanly: assert the composed message contains `UserProfile.DescriptionLength`.
Confirmed the assertion can go red by setting the constant to a different value and watching it fail.

This is the kind of assertion that looks trivial and is not — it is the *only* mechanism that would
catch the drift, because the two values agreeing is currently a coincidence maintained by hand.

## Repair plan

1. RED: a test asserting `Constant.Message.DescriptionTooLong` contains
   `UserProfile.DescriptionLength`. It fails against the current literal only if the values differ,
   which they do not — so **write it against a deliberately wrong value first** to confirm it can
   fail, then restore `500`. A test that cannot be shown to fail is not a test (STANDARD §10).
2. GREEN: change `const string` to `static readonly string` and compose it, matching
   `DisplayNameTooLong`.
3. Check whether `DescriptionTooLong`'s `const`-ness is depended on anywhere — a `const` can be used
   in attribute arguments and `switch` labels, `static readonly` cannot. Grep before changing.

Done. This item has no follow-on work, which is worth noting because it is rare here.

## Out of scope / related

- **Nothing else in `Constant.Message` has this defect**, so the fix does not generalise to a sweep.
  If a later message is added with a literal, that is a new item.
- **The `const` → `static readonly` change is the interesting part of the diff**, so the PR body
  should say why it was necessary rather than leaving a reviewer to wonder why a one-word fix touched
  the declaration kind.

## Close checklist

- [ ] The test was watched failing against a wrong value before being kept
- [ ] `DescriptionTooLong` composes from `UserProfile.DescriptionLength`
- [ ] Grep confirms nothing depended on the `const`
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](../00-debt-log.md)
