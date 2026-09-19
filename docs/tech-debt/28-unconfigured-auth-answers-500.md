# 28 — An unconfigured deployment answers 500 on every protected route, and says it is anonymous

Status: **open** · Kind: correctness · Impact: friction · Area: Api
Source: found 2026-09-19 (during feature 04) · Discharges via: feature 11 (in part) · Opened: 2026-09-19 · Last verified: 2026-09-19

## What the debt is

`AddEntraAuthentication` wires the JwtBearer scheme only when `TenantId` and `Audience` are both
configured. With neither set — the state a fresh deployment is in before it is pointed at a tenant —
it registers bare `AddAuthentication()` and `AddAuthorization()`, and `Program.cs` logs:

> Entra ID authentication is not configured, so every request is anonymous.

**Both halves of that are wrong.** Measured on 2026-09-19 against the real pipeline, through
`TrailBlazeApiFactory`, asking the existing protected controller (`GET /User/me`) with no
authorization header at all:

| `TenantId`/`Audience` | Response |
|---|---|
| absent | **500** — `No authenticationScheme was specified, and there was no DefaultChallengeScheme found` |
| present | **401** |

Nothing is anonymous. There is no scheme, so there is no challenge to issue, and the authorization
middleware throws instead. `app.UseExceptionHandler()` turns that into a problem+json 500.

## Why it matters

**The log line is the more dangerous half.** A start-up warning that says "every request is
anonymous" tells an operator the app is running open in a way they can reason about; the truth is
that every protected route is broken. Both are wrong, but only one of them sounds like something
you can decide to live with until the tenant is set up — and the reassurance is printed at exactly
the moment someone is deciding whether the deployment is safe to leave running.

It is also a **shape** problem rather than a value problem: `/health` answers 200 in both states, so
a liveness probe reports a healthy deployment whose entire protected surface is a 500. An anonymous
GET on the two public read endpoints (features 05/07) would likewise work, so the failure is
invisible from outside until a signed-in route is called.

It affected feature 04 directly: the api tier could not observe a 401 on the activity routes without
first configuring a fake tenant and audience, which is why `ActivityRouteTests` does.

## Where it is

- `src/api/TrailBlaze.Api/ServiceExt.cs` — `AddEntraAuthentication`, the conditional registration
- `src/api/TrailBlaze.Api/Program.cs` — the warning whose claim is false
- `src/api/TrailBlaze.Api/Extensions/` — nothing today asserts either state

## What closing it means

Not settled here, because the two candidate fixes are different products:

1. **Refuse to start** without a tenant and audience, the way the two connection strings already
   are. Consistent with `RequireSetting`, and it makes the deployment mistake a start-up failure
   rather than a runtime one — but it removes the current ability to run the host un-pointed-at-a-
   tenant for `/health` and OpenAPI.
2. **Keep starting, and make the state honest**: register a challenge scheme that denies everything,
   so protected routes answer **401** as a client expects, and correct the warning to say the
   authenticated surface is unavailable rather than anonymous.

Either way the warning text and the observed status must agree, and a test must pin the pair —
that is the assertion feature 11's end-to-end pass and this item's discharge share.
