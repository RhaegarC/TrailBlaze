# 16 — Sign blob read URLs with the application's identity

Status: **Archived** — merged to `develop` in PR #56 · [00-mission-2-sprint.md](00-mission-2-sprint.md)
Source: [PRD](../../PRD.md) — Decision #7 (amended) + "Media storage & delivery"

## Summary

The API signs every blob read URL with the **storage account key**, which it holds as
`BlobConnection` — a shared-key connection string that is a *required* startup setting. So the
deployment carries a secret that never expires, grants full control of every container in the
account, and can only be changed by a deployment.

This was written down as a constraint rather than a surprise, and the remarks that carried it are the
first thing this feature rewrote. `AzureBlobStorageRepository` used to say of itself that it
"requires a **shared-key** connection string: `CreateReadUrlAsync` signs a SAS locally, which needs
the account key. A connection string carrying only a SAS token, or none at all under a managed
identity, cannot sign and will throw on first use — that is a real constraint of this
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

- [07](07-sas-delivery.md) — the mint whose signing this changes
- [12](12-anonymous-media-read.md) — the anonymous read the mint serves; the *service* signs,
  so an anonymous caller is unaffected by the credential change
- [15](15-media-url-cache.md) — puts one URL per item into a listing, which is what makes the
  per-mint cost noticeable enough to cache

## Acceptance criteria

- [x] The composition root takes the storage **account URL** and the application's own identity, and
      `CreateReadUrlAsync` signs with a user delegation key obtained from it. *(2026-10-14 —
      **amended, and the amendment is the honest half of this criterion**: the shared-key connection
      string is still read, as a deliberate override for the emulator and the local stack, neither of
      which has a tenant to authenticate against. So "no account key is read from configuration
      anywhere in the solution" is **not** what this ships and the criterion is not claimed as
      written. What ships is a deployment that sets the account URL and holds no key, and an account
      key that is read only where an operator has chosen to set one.)*
- [x] The setting that replaces `BlobConnection` is still **required**: startup fails with a message
      naming the key when it is absent, exactly as it does today, so the failure stays a boot failure
      rather than a first-request one. *(2026-10-14 — "required" now means **one of the two**, since
      either form is a way to configure storage: blanking both throws at the composition root, and the
      message names `StorageAccountUrl`. `StartupTests`' row moved to that key rather than being
      deleted.)*
- [x] `CreateReadUrlAsync` signs with a user delegation key obtained from the service, and a URL it
      returns is **accepted by the service** for a blob in the private container — with the same blob
      asserted to read nothing without the signature, so the first claim is about the signature rather
      than about a container that turned out to be readable
- [x] **The delegation key is fetched once per key lifetime, not once per URL.** A listing that mints
      one URL per item makes **one** key call for the whole listing. *(2026-10-14 — the count is taken
      **on the wire**, and it has to be: the pipeline caches the bearer token it was handed, so a
      counting `TokenCredential` — the obvious instrument, and the one this test was first written
      with — reports one whether the key is cached or not, and reports one for the write as well. It
      passed the claim and made it unfalsifiable. The write path is asserted to cost no key at all,
      which is what makes the five mints' single fetch the whole of the claim.)*
- [x] A delegation key that has **expired or been revoked is re-fetched**, never reused, so the
      application cannot serve a URL the service will refuse. *(2026-10-14 — asserted on the fetch
      count across a clock that outlasts the reserve, **and** on the read: the URL minted from the
      refetched key is fetched from the service and comes back 200. That second half needed the test
      clock to be set **back** rather than forward from the present — a key is requested from this
      clock and validated by the service against its own, so a clock ahead of the service asks for a
      window that has not opened, which the emulator refuses with a 403. Recorded here because the
      first version of the test failed that way and the cause was the test, not the code.)*
- [x] The SAS's own expiry is unchanged: it still comes from `SignedUrlLifetime`, still clamped to a
      maximum of 60 minutes, which sits far inside a delegation key's own lifetime
- [x] A failure to obtain a delegation key surfaces as an error, never as a URL that carries no
      authorization — the same loud failure the shared-key path gives today
- [x] The **upload and delete** paths work under the identity too. This moves the whole storage
      credential, not only the signing half; a feature that fixed signing and left the data plane on
      the account key would have moved nothing
- [x] The cache is correct on several instances. A delegation key is **not caller-dependent**, so a
      per-instance in-memory cache is sufficient and a distributed cache is deliberately not
      introduced — the opposite of a response cache, whose key has to carry the caller's
      authorization context. The file records that difference rather than leaving it to be
      re-derived (Notes, "What is cacheable here, and what is not")
- [x] The emulator question is settled by evidence and recorded, in the feature file and in the PRD
      if the answer changes the shape of it — see Notes. The answer is **yes**: Azurite 3.37.0 mints a
      user delegation key and accepts a SAS built from it, so the container tier exercises the
      delegation path end to end and no real-account tier is needed

## Tests (TDD)

- **External service, container-tagged** — `UserDelegationSigningTests`: the real
  `AzureBlobStorageRepository` against a live endpoint. A URL signed as the application reads a
  private blob, and the same blob reads nothing unsigned; a write and a delete land under the
  identity; five mints make **one** key request; a key past its reserve is fetched again and the URL
  minted from the new one is accepted by the service; and an identity that cannot sign is an error
  rather than an unsigned URL.
- **Host** — the composition root builds the repository from the account URL with no key, and accepts
  a shared-key connection string in its place; `StartupTests`' required-setting row names
  `StorageAccountUrl`.
- **Unit** — `DelegationKeyLifetimeTests`: the key cache's arithmetic over a clock, so "a key that no
  longer covers the longest URL this application may sign is not reused" is pinned without a service
  call. The repository asserts the same rule through the fetch count, which is what ties the
  arithmetic to the round trip.
- **Not database-tagged.** This feature touches no schema.
- **One claim is made on the wire rather than in the code.** "One fetch per key lifetime" is counted
  by a transport that watches the requests leave, not by a credential double — see
  `CountingTransport` for why the credential cannot make it.
- **One claim is `verification-only`, and it is the deployment's half.** That the ACA-hosted
  application's identity actually holds the delegation permission against a **real** account is not
  establishable from this machine: the tier's answer is the emulator's OAuth, which reads a token's
  claims without verifying them. Verification: not yet run — against a deployed account, set
  `StorageAccountUrl` and nothing else, request a media read URL and fetch it. It is recorded here
  rather than left to be noticed, because it is the one step between "the code is right" and "the
  account agrees".

## Notes / non-goals

- **The emulator question is settled, and the answer is yes.** This was the feature's one blocking
  risk, and it was settled by running it rather than by assuming: **Azurite 3.37.0 mints a user
  delegation key, and accepts a SAS built from one.** The container tier therefore exercises the
  delegation path end to end against a live endpoint — a real key request, a real signature, a real
  fetch — and no real-account tier is needed. What it cost to establish, recorded because none of it
  is guessable from the images:
  - **OAuth requires TLS.** `--oauth basic` answers a plain-HTTP authenticated request with a `403
    AuthenticationFailed`, so the emulator serves HTTPS and every storage test moved with it.
  - **The image ships no certificate and no way to make one** — it has no `openssl`. The stack already
    uses the `azure-cli` image as an init service and that one has openssl 3.3.7, so a one-shot
    service generates a self-signed loopback certificate into a **named volume** before the emulator
    starts, and keeps it between runs so a second `up` does not invalidate the certificate the running
    emulator is already serving. Nothing in it is a secret: the key is unsigned, it names loopback,
    and it goes with the volume.
  - **The certificate is trusted narrowly.** The tier reaches it through a transport that accepts an
    unverifiable certificate **only when the request is going to loopback**, so the same tier pointed
    at a real account by `TRAILBLAZE_STORAGE_CONNECTION` validates that account's certificate normally
    rather than accepting whatever the network presents.
  - **The emulator returns exactly the window it is asked for** — the key's start and end are this
    application's values, not the service's rounding of them.
  - **It enforces the key's own window and nothing more.** A key whose window has not opened is
    refused (403 — the failure that showed the test clock above had to start *behind* the present),
    while a SAS whose expiry sits **past** the key's own is accepted. So "held only while it still
    covers the longest URL this application may sign" is this application's rule rather than one the
    emulator would catch, and the test asserts it on the fetch rather than on the read.
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
- **The local development stack keeps the shared-key path; the test tier does not.** This is the
  substance of the amendment to criterion 1. `docker-compose.yml`'s `blob-init` creates the three
  containers and sets their access levels, needs permission to do both, and has no tenant to
  authenticate against — so it keeps a connection string, and that is what the composition root's
  second form exists for. The **test** tier moved to identity because the delegation path is exactly
  what it exists to exercise, and because it is the one configuration where an identity is available
  without a tenant. Every route in the local stack is still `[Authorize]` with no Entra tenant
  configured, which no test touches and this feature does not change.
- **The account key is not removed from the account** — only from the application's configuration.
  This feature changes what the app needs, not what the account has.
- **Not a step toward public containers.** The `media` container stays private and is never made
  public; nothing here revisits Decision #7's "the container stays private and is never made public".
