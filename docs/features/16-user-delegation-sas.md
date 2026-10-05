# 16 — Sign blob read URLs with the application's identity

Status: **Not started** · [00-mission-2-sprint.md](00-mission-2-sprint.md)
Source: [PRD](../PRD.md) — Decision #7 (amended) + "Media storage & delivery"

## Summary

The API signs every blob read URL with the **storage account key**, which it holds as
`BlobConnection` — a shared-key connection string that is a *required* startup setting. So the
deployment carries a secret that never expires, grants full control of every container in the
account, and can only be changed by a deployment.

This is already written down as a constraint rather than a surprise.
[`AzureBlobStorageRepository`'s own remarks](../../src/api/TrailBlaze.Repository/AzureBlobStorageRepository.cs)
say the type "requires a **shared-key** connection string: `CreateReadUrlAsync` signs a SAS locally,
which needs the account key. A connection string carrying only a SAS token, or none at all under a
managed identity, cannot sign and will throw on first use — that is a real constraint of this
implementation, not an oversight."

This feature removes that constraint. The application authenticates to storage as **itself** — the
identity it already runs as — asks the service for a **user delegation key**, and signs each read
URL with that key instead of the account key.

Three consequences, in order of importance:

1. **The account key leaves configuration.** The deployment needs the account's *URL* and a
   credential it already has; it no longer needs a secret that nothing rotates.
2. **The signatures become revocable.** A delegation key can be revoked at the account, so a leaked
   URL can be cut off — where today the only remedy is rotating the key everything depends on.
3. **Minting stops being free.** `GetUserDelegationKeyAsync` is a **network call**, so the key has
   to be fetched once and cached rather than fetched per URL. This is the one place in Mission 2
   where backend caching is the right answer, and it is why this feature exists separately from
   [15](15-media-url-cache.md) rather than inside it.

## Story

As the operator of this journal I want the application to hold no storage secret at all, so that a
compromised configuration file or container image yields no key that opens every container — and so
that a signature I have already issued can be withdrawn without rotating the credential the whole
application depends on.

## Dependencies

- [07](archive/07-sas-delivery.md) — the mint whose signing this changes
- [12](archive/12-anonymous-media-read.md) — the anonymous read the mint serves; the *service* signs,
  so an anonymous caller is unaffected by the credential change
- [15](15-media-url-cache.md) — puts one URL per item into a listing, which is what makes the
  per-mint cost noticeable enough to cache

## Acceptance criteria

- [ ] The composition root takes the storage **account URL** and a credential, not a shared-key
      connection string; no account key is read from configuration anywhere in the solution
- [ ] The setting that replaces `BlobConnection` is still **required**: startup fails with a message
      naming the key when it is absent, exactly as it does today, so the failure stays a boot failure
      rather than a first-request one
- [ ] `CreateReadUrlAsync` signs with a user delegation key obtained from the service, and a URL it
      returns is **accepted by the service** for a blob in the private container
- [ ] **The delegation key is fetched once per key lifetime, not once per URL.** A listing that mints
      one URL per item makes **one** key call for the whole listing — this is the assertion that
      distinguishes a cache from a per-mint round trip, and a test that mints one URL cannot make it
- [ ] A delegation key that has **expired or been revoked is re-fetched**, never reused, so the
      application cannot serve a URL the service will refuse
- [ ] The SAS's own expiry is unchanged: it still comes from `SignedUrlLifetime`, still clamped to a
      maximum of 60 minutes, which sits far inside a delegation key's own lifetime
- [ ] A failure to obtain a delegation key surfaces as an error, never as a URL that carries no
      authorization — the same loud failure the shared-key path gives today
- [ ] The **upload and delete** paths work under the identity too. This moves the whole storage
      credential, not only the signing half; a feature that fixed signing and left the data plane on
      the account key would have moved nothing
- [ ] The cache is correct on several instances. A delegation key is **not caller-dependent**, so a
      per-instance in-memory cache is sufficient and a distributed cache is deliberately not
      introduced — the opposite of a response cache, whose key has to carry the caller's
      authorization context. The file records that difference rather than leaving it to be
      re-derived (Notes, "What is cacheable here, and what is not")
- [ ] The emulator question is settled by evidence and recorded, in the feature file and in the PRD
      if the answer changes the shape of it — see Notes

## Tests (TDD)

- **External service, container-tagged** — the real `AzureBlobStorageRepository` against a live
  endpoint: a minted URL is accepted for a private blob, the key is fetched once across many mints,
  and an expired key is re-fetched rather than reused. Whether this tier can run against the
  emulator at all is the open question in Notes, and it is the first thing this feature settles.
- **Host** — the composition root resolves the credential, and still fails startup naming the key
  when the account setting is absent. `StartupTests`' current assertion that `BlobConnection` is
  required moves to the replacement rather than being deleted.
- **Unit** — the key cache's arithmetic over a clock: a key inside its window is reused, one at or
  past its window is not, so the refetch rule is pinned without a service call.
- **Not database-tagged.** This feature touches no schema.

## Notes / non-goals

- **The emulator is the central risk, and this document does not assume it away.** The container
  tier runs the real repository against Azurite, and the repository's rule is that **no fake stands
  in for the store** ([testing-and-tdd.md](../testing-and-tdd.md)). The user-delegation-key API
  requires an OAuth-authenticated request, and whether Azurite implements it, in the version this
  repository pins, is **not asserted here** — it is to be established by running it. If Azurite
  cannot mint a delegation key, then the delegation path is exercised by **no tier**: the emulator
  would prove the shared-key path and the deployed account the delegation one, and the two are not
  then "the same code, a different account behind it" in the sense the test strategy requires. That
  answer decides whether this feature is adoptable as written or needs a real-account tier, and it
  comes before any code.
- **This is not a performance feature.** It is in Mission 2 because it is the change that *creates*
  the backend caching requirement [15](15-media-url-cache.md) does not have, and because the two are
  read together: 15 removes N round trips from the browser and adds N signatures on the server, and
  16 is what makes those signatures cost a network call if they are not cached.
- **What is cacheable here, and what is not.** The line is caller-dependence, and it is worth drawing
  explicitly, because this feature adds a cache where the rest of the mission deliberately adds none.
  - **The delegation key is caller-independent.** It belongs to the account, so every caller's URL is
    signed with the same key and one instance may hold one key and serve every request from it. A
    per-instance `IMemoryCache` is correct and a distributed cache is unnecessary — several replicas
    each holding their own copy are all right, which is the opposite of a case that needs Redis.
    *(15's boundary rounding makes two mints in one minute the same **string**; it does not make the
    URL caller-independent, and the two should not be read as the same claim.)*
  - **The reads it signs are caller-dependent.** The same route yields a different payload to an
    anonymous, a signed-in and an owner caller (Decisions #26, #30), and a listing's URLs sit inside
    that payload. So a correct response cache would have to carry the caller's authorization context
    in its key: for a `Public` entry two anonymous callers could share a listing safely, while for
    `Shared` and `Private` the key becomes effectively per-caller — which is exactly where the payload
    is largest and where a cache would most want to help. A per-caller entry hits rarely on its own
    replica, and a key that omitted the caller would hand one caller another's private URL.
  - **So caching a read route as part of this work is a finding, not an optimisation.** Nothing here
    changes who may read anything; it changes which key signs the token they read through. A payload
    cache appearing alongside this change would be caching the wrong axis.
  - **The mint path ends up costing what it costs today.** Once the key is held, signing is local
    again, so N URLs cost one key fetch amortised over its lifetime plus N local HMACs.
    `CreateReadUrlAsync` already returns `Task<Uri>` while completing synchronously, so awaiting a
    key fetch is not a signature change and no call site moves.
- **No new route, no schema change, no payload change.** Nothing a caller sees moves; the SAS is
  still a bearer token whose expiry is still the control (Decision #7).
- **The local stack and the test tier keep a shared-key path** unless the emulator can be driven
  with identity. The compose stack's `blob-init` creates the three containers and sets their access
  levels, and those operations need the identity to hold that permission too.
- **The account key is not removed from the account** — only from the application's configuration.
  This feature changes what the app needs, not what the account has.
- **Not a step toward public containers.** The `media` container stays private and is never made
  public; nothing here revisits Decision #7's "the container stays private and is never made public".
