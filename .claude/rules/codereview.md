# Code review

## Review against the deployment target

Verify a design against the environment it actually runs in — not the one the scaffold assumed,
and not the developer's laptop.

Ask, of anything that starts up, writes, or holds state:

- **What is it deployed to?** A `docker-compose.yml` was written and then removed: it existed
  because the inherited scaffold implied a local stack, while the real targets are Azure Container
  Apps for the API and Azure Static Web Apps for the web app. Neither consumes a compose file, so
  there was no stack to orchestrate.
- **How many of it runs?** A design correct on one instance can be wrong on many. Migrations
  applied by an `IHostedService` at startup were correct until ACA ran several replicas, which race
  each other over the same DDL. The same reasoning applies to feature 03's startup admin seeding.
- **What does it reach over the network?** Azure SQL Database is managed and has no local
  stand-in, so "it works on my machine" was never an available fallback — the connection string,
  the firewall, and the database's existence are all prerequisites rather than things the app
  arranges.

A design that has not been checked against its target is unverified, however green its tests are.
Tests here run with no database and a fake storage service, so they cannot speak to deployment
shape at all.

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
`IStorageRepository` beside `IDbRepository` and `IUserRepository` is where a reader looks, and
nothing about "storage" makes it an environment detail.

`TrailBlaze.Interface/Infrastructure/` is the one legitimate third folder, and it is narrow: it
holds contracts that describe the *runtime the code is executing in* rather than an operation
against anything. `IUserContextService` is the example — it reports who the caller is, and no
layer performs it. Treat that folder as a closed set: a new contract belongs in it only when the
contract is genuinely about the ambient environment, and "it does some I/O" or "it is a technical
concern" is not that. `IStorageRepository` was in it on exactly that reasoning and did not belong.

**How to apply:** for every type the change adds or renames, name the layer it belongs to, check
the file and type name carry that layer's suffix, and check the contract sits in the matching
`TrailBlaze.Interface` folder. Do this while reading the diff, not afterwards — the wrong suffix
is cheap to fix in review and expensive once later features have copied it. `AzureBlobStorageService`
sat in `TrailBlaze.Repository` with its interface in `TrailBlaze.Interface/Infrastructure/`; both
were found by reading the operations the type exposes (upload, delete, move, mint a read URL) and
seeing that every one of them was data access.
