# Code review

## Review against the deployment target

Verify a design against the environment it actually runs in — not the one the scaffold assumed,
and not the developer's laptop.

Ask, of anything that starts up, writes, or holds state:

- **What is it deployed to?** A `docker-compose.yml` was written and then removed: it existed
  because the inherited scaffold implied a local stack, while the real targets are Azure Container
  Apps for the API and Azure Static Web Apps for the web app. Neither consumes a compose file, so
  there was no stack to orchestrate. (`src/api/docker-compose.test.yml` exists and is not that: it
  starts no application process and is not deployed anywhere.)
- **How many of it runs?** A design correct on one instance can be wrong on many. Migrations
  applied by an `IHostedService` at startup were correct until ACA ran several replicas, which race
  each other over the same DDL. Feature 03's startup admin seeder was the second instance of the
  same reasoning and was removed in 2026-09-19 for it — any write an application performs at boot is
  a write every replica performs at boot, and the more security-relevant the column, the worse the
  race.
- **What does it reach over the network?** Azure SQL Database is managed and has no local
  stand-in, so "it works on my machine" was never an available fallback — the connection string,
  the firewall, and the database's existence are all prerequisites rather than things the app
  arranges. (The *test tier* has containers that stand in for both services; the deployed
  application still has none, which is the case that matters here.)

A design that has not been checked against its target is unverified, however green its tests are.

**The test tier has since grown a real engine and a real blob service, and that does not settle this
question.** `docker-compose.test.yml` starts SQL Edge and Azurite so the database and storage tiers
have something to talk to, so migrations, schema bounds and SAS round-trips are now *executed*
rather than simulated — a real gain, and one that removed two fakes. But one container is not
several ACA replicas, an emulator's certificate and ACL behaviour are not a real account's, and no
test signs in through Entra. **The suite can now speak to engine shape, and still cannot speak to
deployment shape** — so this question is still asked of the design, not of the test run.

**How to apply:** when reviewing a change that runs on a host, say which host and how many
instances, and check the design against that. Do this *before* reviewing the code inside it — two
features of the foundation feature were rewritten because this question had not been asked.

## Review the layer a type belongs to, and the suffix that names it

Three layers, and every type belongs to exactly one of them:

1. **`TrailBlaze.Api`** — the entrance of the application. Controllers, the composition root, and
   the wiring around them. An entry point routes and delegates; it holds no business rule and no
   data access of its own.
2. **`TrailBlaze.Service`** — business logic. Files are named **`XXXService`**.
3. **`TrailBlaze.Repository`** — data operations: the database, blob storage, and anything else
   outside the process. Files are named **`XXXRepository`**.

The suffix is the layer's declaration of intent, which is why a mismatch is a review finding rather
than a style preference. A `XXXService` file inside `TrailBlaze.Repository` reads as a type that
belongs a layer above the one holding it, and a reader will go looking for it there; a
`XXXRepository` in `TrailBlaze.Service` claims data access in the layer that is supposed to own
only rules. Both make the layout lie about the dependency direction the solution is built on.

The contract mirrors its implementation: a repository contract lives in
`TrailBlaze.Interface/Repository/`, a service contract in `TrailBlaze.Interface/Service/`. The
folder follows the kind of the contract, not the suffix someone happened to give it — an
`IStorageRepository` beside `IDbRepository` is where a reader looks, and
nothing about "storage" makes it an environment detail.

`TrailBlaze.Interface/Infrastructure/` is the one legitimate third folder, and it is narrow: it
holds contracts that describe the *runtime the code is executing in* rather than an operation
against anything. `IUserContextService` is the example — it reports who the caller is, and no
layer performs it. Treat that folder as a closed set: a new contract belongs in it only when the
contract is genuinely about the ambient environment, and "it does some I/O" or "it is a technical
concern" is not that. `IStorageRepository` was in it on exactly that reasoning and did not belong.

**Registration belongs in the composition root, not in an extension method of the layer being
registered.** A bare `services.AddSingleton<...>()` parked in `TrailBlaze.Repository` hides the
composition from the only place that composes anything, so register it inline in
`TrailBlaze.Api/ServiceExt.cs` and delete the helper — `StorageExtensions.AddBlobStorage` was one
such helper and is gone.

The test is **whether the composition root calls it**, not whether a helper exists somewhere.
`PersistenceExtensions.AddRepositoryPersistence` is the one documented exception and stays in
`TrailBlaze.Repository`, for four reasons that do not apply to a registration one-liner: it does
configuration work rather than registration (`UseSqlServer`, resolving the interceptor through the
service provider, setting the `DbContext` lifetime); it needs the `internal`
`AuditSaveChangesInterceptor`, which only `TrailBlaze.Repository.Test` is granted access to;
that test tier calls it to build the no-database harness
([STANDARD.md](../../src/api/STANDARD.md) §10), and `TrailBlaze.Repository.Test` does not reference
`TrailBlaze.Api`, so a move would leave the harness unable to reach it and force the context to be
hand-built — the exact thing that pattern exists to prevent; and inlining it would put the EF
provider into the Api layer.

So the rule is: inline a bare registration; leave a helper that configures the layer, is called
from more than one tier, or depends on a type its caller cannot see.

**How to apply:** for every type the change adds or renames, name the layer it belongs to, check
the file and type name carry that layer's suffix, and check the contract sits in the matching
`TrailBlaze.Interface` folder. Then ask where it is registered, and whether that place is the
composition root or a helper the layer kept for itself — before assuming the helper is the same
kind of thing as the last one removed. Do this while reading the diff, not afterwards: the wrong
suffix is cheap to fix in review and expensive once later features have copied it.
`AzureBlobStorageService` sat in `TrailBlaze.Repository` with its interface in
`TrailBlaze.Interface/Infrastructure/`; both were found by reading the operations the type exposes
(upload, delete, move, mint a read URL) and seeing that every one of them was data access. Its
`AddBlobStorage` neighbour looked like the same case as `AddRepositoryPersistence` and was not.
