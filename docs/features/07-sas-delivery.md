# 07 — SAS Delivery

Status: **Not started** · [00-mission-1-sprint.md](00-mission-1-sprint.md)
Source: [PRD](../PRD.md) — Decisions #2/#5/#6/#7 + "Media storage & delivery" + "API surface" + "Authentication & authorization".

## Summary

The only way the private container's bytes ever reach a browser. `GET /api/media/{id}/url` mints a
**short-lived SAS URL** for one blob, for an authenticated caller, and the container itself stays
private. This is the security hot spot of the media path: the URL is a bearer token, so the
**expiry window is the real control**, and an unauthenticated request must be rejected with 401
**before any blob operation is attempted**.

## Story

As a signed-in user I want a short-lived URL for a media item so that the browser can play an
image or video that is otherwise private.

## Dependencies

- [06-media-upload](06-media-upload.md) (the `media` rows and the private blobs being addressed)
- [02-entra-auth](archive/02-entra-auth.md) (caller identity — the endpoint is not anonymous)
- [01-foundation](archive/01-foundation.md) (`IStorageRepository` abstraction + the fake; the tagged storage tier)

## Acceptance criteria

- [ ] `GET /api/media/{id}/url` requires a valid Entra bearer token; a request with no token returns **401** (PRD permission table)
- [ ] The 401 is produced by the authorization layer **before any `IStorageRepository` call** — asserted with a fake that records or throws on invocation, so "rejected first" is a tested property, not a code-reading claim
- [ ] A request with a valid token but an unknown media id returns 404, and that check also precedes any minting
- [ ] The response carries the SAS URL and its expiry as a UTC instant, so the client can refresh before it lapses
- [ ] The TTL comes from configuration with a hard maximum: a configured value above the cap is **clamped, not honoured**
- [ ] Expiry is **bounded and asserted in tests** — a test fails if the window is absent, non-positive, or longer than the cap. The expiry returned must equal the expiry actually embedded in the token
- [ ] The SAS grants **read only** — no write, create, or delete permission
- [ ] The SAS is scoped to the **single blob**, not to the container
- [ ] The SAS is minted against the **private** container only, and never against a blob in a
      **public** container. Note where that line now falls: since Decision #29 `covers` holds only
      `Public` activities' covers, while a `Shared` or `Private` activity's cover lives in `media`
      and **does** get a SAS (feature 08). The rule is therefore about *containers*, not about
      "covers" — a `covers` blob is world-readable by design (Decision #13, amended by #29) and a
      SAS there would add a credential to content that needs none
- [ ] Minting for a cover in the private container is the **same** operation as for a media blob,
      reached through the same repository method — feature 05 and feature 08 must not construct SAS
      URLs by a second path, or the TTL cap and the read-only scope stop being single-sourced
- [ ] A URL for a blob whose bytes are no longer present is the client's problem, not a minting failure — the failure mode is documented and does not 500
- [ ] Minting goes through `IStorageRepository`, so unit tests run against the in-memory fake and the Azure-specific construction is covered by the tagged tier
- [ ] The generated URL is not written to logs (it is a credential)

## Tests (TDD)

- Unit (`TrailBlaze.Service.Test`) **hot spot (security — must be test-first, RED first):**
  - an unauthenticated caller is rejected and the fake `IStorageRepository` records **zero** interactions — the "before any blob operation" claim is the assertion;
  - a successful mint produces a URL whose expiry is in the future and within the cap;
  - the requested permission set is read-only and the target is a single blob in the private container;
  - an over-long configured TTL is clamped to the maximum.
- Integration (`TrailBlaze.Repository.Test`): with nothing listening on a port, the media row is the source of the blob path — the lookup whose id is soft-deleted or unknown is asserted to yield no blob path, and therefore no URL, with the query inspected via `ToQueryString()` ([testing-and-tdd.md](../testing-and-tdd.md)).
- Integration (`TrailBlaze.Api.Test`): a tokenless request to `/api/media/{id}/url` returns 401 with no storage call; an authenticated request returns 200 with a URL and an expiry.
- Storage integration (`TrailBlaze.Service.Test`, tagged `Category=StorageIntegration`; `dotnet test --filter Category=StorageIntegration`): a **real SAS round-trip** — mint against real Azure, issue a plain HTTP GET against the returned URL with no credentials, and receive 200 with the original bytes; then request a URL whose expiry has already passed and observe the storage service **refuse** it. This is the tier that proves SAS generation, which the fake by construction cannot (see [testing-and-tdd.md](../testing-and-tdd.md)).

## Notes / non-goals

- **The SAS URL is a bearer token.** Anyone holding the string can read that blob until it expires; the URL's secrecy is not a control, so the **expiry window is the real control**. The consequences this feature accepts: a short TTL, a hard server-side cap, no long-lived SAS values persisted anywhere, no full URLs logged, and no SAS embedded in stored data.
- **No authorization check here beyond authentication.** Any signed-in user may currently obtain a URL for any media item. Both halves of the matrix arrive later and neither is present in this feature: the **visibility** gate — a caller who cannot read the activity gets **404**, not a URL (Decisions #26/#29) — is applied by features [05](05-public-activity-list.md)/[09](09-permission-enforcement.md), and the owner/admin rules by 09. This feature is accordingly **not safe to deploy** on its own: the authentication boundary is real, the authorization one arrives in 09. Stated with the visibility half included because a private activity's media being mint-able by any signed-in stranger is exactly the leak a reader might assume 05 had already closed.
- No revocation list, no one-time-use tokens, and no forced re-mint on download — with a bounded TTL, expiry is the only lever and that is deliberate.
- No CDN, no range-request tuning, no download throttling or bandwidth accounting.
- No SAS for a blob in a **public** container (`covers`, `avatars`) — a SAS there would add a
  credential to content that needs none. This is a statement about containers, not about cover
  images as a category: a `Shared`/`Private` activity's cover is a `media` blob and is served by
  SAS like any other (Decision #29).
