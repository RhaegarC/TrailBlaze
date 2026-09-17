# 07 — The `.http` file requests `/weatherforecast/`, which does not exist

Status: **open** · Kind: hygiene · Impact: cosmetic · Area: Api
Source: STANDARD §12.7 · Discharges via: — (orphaned) · Opened: 2026-09-17 · Last verified: 2026-09-17

## What the debt is

[TrailBlaze.Api.http:3](../../src/api/TrailBlaze.Api/TrailBlaze.Api.http#L3) contains:

```
GET {{TrailBlaze.Api_HostAddress}}/weatherforecast/
```

There is no `WeatherForecast` controller in this solution and has not been since the scaffold was
replaced. The file is the scaffold's sample request, left behind.

## Why it matters

It is the first thing a new developer runs. The `.http` file exists precisely to be the zero-setup
way to hit the API, and the zero-setup way 404s — which reads as "the API is not running" or "the
route is wrong" rather than "this line is stale". The cost is small and paid repeatedly, once per
person, at the moment they know least about the codebase.

It is also a signal: a sample request for an endpoint that never existed beside real endpoints means
nobody has opened this file since the scaffold. After the fix it should carry the routes that
actually matter — which makes it useful, not merely less wrong.

## Evidence

Checked against the code 2026-09-17.

- The line is as quoted; no `WeatherForecast` type exists anywhere in `src/api/`.
- The API's real surface is small and named in the PRD: `/health`, and the four `/user/me` routes
  ([PRD.md:359-362](../PRD.md#L359-L362)) — all of which require a bearer token, which the file
  should say.

## Testability

**verification-only.** There is nothing to assert: a `.http` file is not compiled, not executed by
the test suite, and has no behaviour a test could observe. Inventing an assertion over its contents
would produce a test that cannot fail, which STANDARD §10 explicitly rules out.

The close checklist therefore requires a stated verification instead of a test: request the URL the
file names and observe the 404, then request the URL written in its place and observe the response.
Record both in the PR body under "No test — and why".

## Repair plan

1. Replace the `weatherforecast` request with requests for endpoints that exist. The useful set is
   `GET /health` (no token, so it works immediately) plus the four profile routes.
2. **The profile routes need a token, and the file should say so** — a request line that returns 401
   for an unexplained reason is the same trap as one that returns 404. Add a comment naming how to
   get a token, or scope the file to `/health` and reference the PRD for the rest. The honest minimum
   is a file whose every line works as written, with a comment on the ones that need a token.
3. If item [04](04-usercontroller-route-convention.md) lands first, use the paths as *documented*
   there, not as currently served — otherwise this fix goes stale in the same way.

## Out of scope / related

- **Item [04](04-usercontroller-route-convention.md)** changes the profile route casing. If both are
  open, do 07 against the resolved path rather than the current one, or do 07 after it.
- **Item [19](19-doc-indexes-drifted.md)** covers documentation that describes a state the code has
  left. This is the same class, one file smaller, and deliberately kept separate because it is in
  `src/` rather than `docs/` and has a different reviewer.

## Close checklist

- [ ] Every request line in the file names an endpoint that exists
- [ ] Lines needing a bearer token say so, so a 401 is not read as a broken file
- [ ] `Verification:` line in this file recording what was requested and what came back
- [ ] "No test — and why" section in the PR body
- [ ] Moved to `archive/`, row updated in [00-debt-log.md](../00-debt-log.md)
