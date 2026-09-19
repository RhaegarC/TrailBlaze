# 21 — The public/private container set exists only in prose

Status: **open** · Kind: capability · Impact: silent-wrong · Area: Storage
Source: found 2026-09-18 (during the container-backed test change) · Discharges via: — (orphaned) · Opened: 2026-09-18 · Last verified: 2026-09-18

## What the debt is

Three containers make up the closed set, and their access levels are declared in XML comments on
`Constant.StorageContainer` and **nowhere else**:

| Container | Documented as | Declared by anything executable? |
|---|---|---|
| `covers` | public | no |
| `avatars` | public | no |
| `media` | private | no |

Until 2026-09-18, `SetAccessPolicy` and `PublicAccessType` appeared **nowhere in the solution at
all**. They now appear in exactly one place — `TrailBlaze.Repository.Test`'s `AzureStorageFixture`,
which sets each level for the emulator — and that does not help a real account, because the fixture
sets them **only when the endpoint is loopback**. That restriction is deliberate and correct (a test
run must never flip a real container's ACL), but it means the executable statement of this set lives
in the one place deliberately forbidden from acting on it.

## Why it matters

**`CreatePublicUrl` is called in production, and nothing makes its URL work.**
[`UserService.cs:232`](../../src/api/TrailBlaze.Service/UserService.cs#L232) builds an avatar URL
with it, and that value reaches `UserProfileResponse.AvatarUrl`. `CreatePublicUrl` constructs an
unsigned URL from the endpoint and the path; it asks the account nothing. So if `avatars` is not
actually public, the response carries a well-formed URL that returns an error on fetch — the profile
shows a broken image, and nothing in this repository fails.

**This fails closed, which is why it is debt and not a bug.** An unset ACL makes a container
*private*, so the failure mode is a 404, not a leak: private media does not become readable. Under
the register's own boundary — a bug is something failing now, or something that lets private media
reach the wrong caller — neither holds. The cost is a silently broken public URL, which is
`silent-wrong`.

**And it is invisible in every environment this repo can run.** The emulator gets its ACLs set by the
fixture, so the container tier's public-read assertions pass; a real account is untouched. The suite
is green and the deployment is unconfigured, simultaneously.

## Evidence

Checked 2026-09-18.

- `grep -rn "SetAccessPolicy|PublicAccessType|AccessLevel" src/api --include=*.cs` returns six hits,
  all in `TrailBlaze.Repository.Test` — `StorageContainerRoutingTests.cs` and
  `AzureStorageFixture.cs`. **Zero in `TrailBlaze.Repository` or `TrailBlaze.Api`.**
- `grep -rn "CreatePublicUrl" src/api --include=*.cs` returns one production call site —
  `TrailBlaze.Service/UserService.cs:232` — plus the interface, the implementation, and the tier.
- `Constant.StorageContainer`'s comments state the levels; the type carries no value that encodes
  them.
- README says the containers "must exist in your Azure Storage account — the API reads and writes
  blobs but never provisions containers" ([README.md:69-71](../../README.md#L69-L71)), so by the
  repo's own account nothing in the application will set these.

## Testability

**verification-only**, and the reason is narrow rather than a convenience. The property under repair
is "the real account's three containers have the intended access levels". That is observable only
against a real account, and this repository deliberately does not provision one — the container tier
is permitted to observe a real account but, by its own safety rule, not to change it. So the repair
is verified by running the steps and recording what was observed.

The emulator half is already covered and should not be counted twice:
`StorageContainerRoutingTests.A_public_container_serves_its_object_to_an_unsigned_url` and
`The_media_container_is_private_and_the_other_two_are_public` assert these levels against Azurite.
Those prove the *intent* is achievable, not that any deployment realises it.

## Repair plan

1. **Decide who sets the levels, because the fix differs by the answer.**
   - *(a)* **Provisioning is a deployment concern** (the README's current position) → write the steps
     down where a deployment will read them, and give `Constant.StorageContainer` a machine-readable
     partner so the set is not prose. Cheapest; leaves the value unchecked.
   - *(b)* **The API asserts them at startup** → a startup check that each container exists and has
     the expected level, failing with a message naming the container. This turns a runtime 404 into a
     boot failure, and fits the reading of `RequireSetting` that a missing prerequisite should be
     loud. It is a deployment-shape change and needs the replicas question asked
     ([codereview.md](../../.claude/rules/codereview.md)).
   - *(c)* **The API sets them at startup** → rejected on the grounds feature 03's startup admin
     seeder was removed for on 2026-09-19, one feature after this item was written: several ACA
     replicas race, and an application silently rewriting a security setting on its own is the side
     effect this item exists to complain about.
2. Whichever is chosen, the levels must become a **value in code** — a single map from container to
   level that `CreatePublicUrl`'s callers, any startup check, and the test fixture all read. Today
   three of those four places either guess or repeat the prose.
3. `Verification:` line recording the real account's observed levels per container.
4. Do **not** fix this inside the container-backed-test change. That change made the gap visible; it
   did not cause it, and the fix is a deployment decision.

**Recommendation: (b), with (a) written down as the interim.** A startup assertion is the only option
that makes the failure arrive at the moment the mistake is made rather than the first time someone
loads an avatar, and it is a read, not a write, so the replicas objection does not apply.

## Out of scope / related

- **Private media staying private is not in question here.** `media` is not given a public URL
  anywhere; the risk in this item is a broken public URL, not a readable private one.
- **[Feature 07](../features/07-sas-delivery.md)** owns which containers get a SAS and which get a
  plain URL. This item owns whether the container's own level matches that choice.
- **[Feature 08](../features/08-cover-images.md)** routes covers between `covers` and `media` by
  activity type and depends on both levels being real.

## Close checklist

- [ ] Each container's access level is a value in code, not only a comment
- [ ] A named owner sets them — deployment step or startup assertion — and the choice is recorded
- [ ] A `Verification:` line records the levels observed on a real account
- [ ] `CreatePublicUrl`'s one production caller has a working URL, or its failure is loud
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
