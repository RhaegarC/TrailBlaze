# 17 — `AllowedOrigins` is declared twice, and one copy silently wins

Status: **open** · Kind: correctness · Impact: silent-wrong · Area: Config
Source: found 2026-09-17 (not a §12 item) · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

`AllowedOrigins` is set in two version-controlled files:

**1.** [appsettings.Development.json:8](../../src/api/TrailBlaze.Api/appsettings.Development.json#L8)

```json
"AllowedOrigins": "https://localhost:8086,http://localhost:8080"
```

**2.** [launchSettings.json:20](../../src/api/TrailBlaze.Api/Properties/launchSettings.json#L20) — the
`https` profile, as an environment variable, with the **identical** value:

```json
"AllowedOrigins": "https://localhost:8086,http://localhost:8080"
```

The two launch profiles do not agree with each other:

| Profile | `AllowedOrigins` | Where it resolves from |
|---|---|---|
| `https` | `https://localhost:8086,http://localhost:8080` | its own env var — **wins over appsettings** |
| `http` | `https://localhost:8086,http://localhost:8080` | `appsettings.Development.json` — nothing overrides it |

## Why it matters

**The `https` copy is dead and does not say so.** .NET configuration layers environment variables
over JSON, so launching the `https` profile takes the `launchSettings` value and the
`appsettings.Development.json` value is never read. A developer who edits the JSON — the file that
*looks* like the configuration — changes nothing, and gets no feedback that they edited a losing
copy.

**The two files are also a documented violation of the project's own rule.** README says the
required settings `DbConnection`, `BlobConnection` and `AllowedOrigins` come from user-secrets
"never from `appsettings.json` or `launchSettings.json`, which are version-controlled"
([README.md:47-49](../../README.md#L47-L49)). `AllowedOrigins` is in both of them. So the rule as
written is either wrong or is being broken, and nothing distinguishes the two readings.

**And `AllowedOrigins` is the one of the three that is a real deployment setting.** It is
`RequireSetting`-ed at startup alongside the two connection strings — so it has a production value —
but unlike them it is *not a secret*. That is the likely reason it drifted into the repo while the
other two did not, and it is the reason the README's blanket rule does not fit it. The rule and the
files disagree because the rule did not anticipate a non-secret required setting.

**The `http` profile inherits a value written for the `https` profile.** `https://localhost:8086` is
an origin the `http` profile does not serve. CORS permits an origin nobody browses from, so nothing
fails — which is why this has survived. The value is simply not about the profile reading it.

## Evidence

Checked against both files 2026-09-17.

- `appsettings.Development.json:8` and `launchSettings.json:20` hold byte-identical values.
- `launchSettings.json`'s `http` profile lists only `ASPNETCORE_ENVIRONMENT` and sets **no**
  `AllowedOrigins`; its `applicationUrl` is `http://localhost:8080`.
- `launchSettings.json`'s `https` profile sets `applicationUrl` to
  `https://localhost:8086;http://localhost:8080` and `AllowedOrigins` to both of those.
- README:47-49 states the never-from-these-files rule; README:48 lists `AllowedOrigins` among the
  three required settings, in the same sentence that the two files contradict.

## Testability

**testable**, and cheaper than it looks. The assertion is on resolved configuration, not on a
running host: build the configuration the way the host does, layering
`appsettings.Development.json` under a profile's environment variables, and assert the value that
`RequireSetting` will read. That goes red on a reverted fix, which is the standard the fix has to
meet.

The existing Api-tier startup tests already assert `RequireSetting` behaviour, so the harness for
reading resolved config exists; this adds a case rather than a tier.

## Repair plan

1. **Decide the rule first, because the fix differs by the answer.**
   - *(a)* `AllowedOrigins` is ordinary non-secret config → delete the `launchSettings.json` entry,
     keep `appsettings.Development.json`, and **amend the README rule** to say it applies to
     credentials (`DbConnection`, `BlobConnection`) rather than to every required setting.
   - *(b)* Every required setting lives in user-secrets → move `AllowedOrigins` there, delete both
     copies, and keep the README rule as written.
2. RED: a test asserting the resolved `AllowedOrigins` for a named profile, written against the
   current double-declaration so it is seen to fail for the right reason.
3. Apply the chosen fix. Under (a) the `http` profile then inherits a value naming only its own
   origin, or inherits the shared dev value deliberately — say which.
4. Correct the README either way. It is the document that made this look settled.

**Recommendation: (a).** `AllowedOrigins` is a permitted-origin list, not a credential; putting it in
user-secrets would make every developer's CORS config private and unshared, which is the opposite of
what a local dev origin list is for. The README's rule is the thing that is wrong.

## Out of scope / related

- **Item [19](19-doc-indexes-drifted.md)** owns the README drift generally. This item owns the one
  README sentence that is load-bearing for its own repair — the two are the same fix and should not
  be split across two PRs.
- **Deployed `AllowedOrigins` is a separate question** — in ACA it comes from app settings, not from
  anything here. This item is about the two local files.
- **`Cors` policy wiring is not in scope.** Whether the list is split correctly
  (`Blazor`/`WebApp` policy names) is a different question from where the value is declared.

## Close checklist

- [ ] The README rule and the files agree — one of them was changed to match the other
- [ ] `AllowedOrigins` has exactly one local declaration, or the reason for two is written down
- [ ] A test asserts the resolved value for at least one named profile
- [ ] Each launch profile's effective origin set is stated, including `http`'s
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](00-debt-log.md)
