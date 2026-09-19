# Backend API Development Standard

This document is the standard for backend APIs built on this solution. It is shipped inside
the project, so it travels with the code: cite it in review, and if you find yourself working
around a rule here, change the rule in a pull request rather than in one service.

It is prescriptive. Where a rule exists because of a specific past failure, the reason is
given — not to justify the rule, but because a rule whose reasoning is missing gets
"simplified" away by the next person.

**Reading it:** sections 1–2 are the layout and the one workflow you will use most. Sections
3–9 are the rules by area. Section 10 is testing, 11 is CI, and **12 points at the tech-debt
register, which lists the places this standard and the code do not yet agree** — read that one
before trusting the rest blindly.

---

## 1. Solution layout

| Project | Responsibility | May reference |
| --- | --- | --- |
| `TrailBlaze.Model` | Entities, DTOs, constants, attributes. No behaviour. | *nothing* |
| `TrailBlaze.Interface` | The contracts: repository and service interfaces, shared abstractions. | `TrailBlaze.Model` |
| `TrailBlaze.Service` | Business logic. Implements `TrailBlaze.Interface` service contracts. | `TrailBlaze.Interface`, `TrailBlaze.Model` |
| `TrailBlaze.Repository` | Data access. Implements `TrailBlaze.Interface` repository contracts. | `TrailBlaze.Interface`, `TrailBlaze.Model` |
| `TrailBlaze.Api` | Composition root: hosting, DI wiring, controllers, middleware, configuration. | all of the above |
| `TrailBlaze.Api.Test`, `TrailBlaze.Repository.Test`, `TrailBlaze.Service.Test` | xUnit tests — one project per layer, holding the tests for that layer. | the layer each project tests, plus `TrailBlaze.Interface` and `TrailBlaze.Model` |

Dependencies point **inward**. `TrailBlaze.Interface` is the pivot: both `Service` and `Repository`
depend on it because both *implement* it, and neither knows the other exists. `TrailBlaze.Api` is the
only project that knows how the parts fit together.

```
        TrailBlaze.Api  ──────────────► everything
           │
           ├──► TrailBlaze.Service ──┐
           │                  ├──► TrailBlaze.Interface ──► TrailBlaze.Model
           └──► TrailBlaze.Repository┘
```

### Rules

- **`TrailBlaze.Model` must not gain a project reference.** It is the bottom of the graph. The
  moment it depends on something, every project depends on that something.
- **`TrailBlaze.Model` must not gain a package reference to a framework.** No EF Core, no ASP.NET
  Core. It is plain data, which is why it can be referenced from anywhere.
- **`TrailBlaze.Service` must not reference `TrailBlaze.Repository`.** A service depends on
  `TrailBlaze.Interface`, never on a concrete data-access type. If a service needs data, it asks for
  an interface.
- **`TrailBlaze.Repository` must not reference `TrailBlaze.Service`.** Same rule, other direction.
- **Only `TrailBlaze.Api` reads configuration.** See section 6.
- Place a contract in `TrailBlaze.Interface` under the folder matching its kind — `Repository/`,
  `Service/`, `Infrastructure/`. `IUserContextService` is in `Infrastructure/` and not
  `Service/` because it describes the runtime environment rather than a business capability.
  `IStorageRepository` is in `Repository/` because it is the opposite case: it performs data
  operations against a store — upload, delete, move, mint a read URL — so its kind is data
  access, and it sits beside `IDbRepository`. The folder follows the kind of the contract, not
  the suffix on its name. `IUploadValidationService` is in `Service/` for the same reason in the
  other direction: it applies the app's upload rules and touches no store.
- **A service is depended on through its interface.** Every type in `TrailBlaze.Service` declares
  one in `TrailBlaze.Interface/Service/` and is registered by it in the composition root —
  `AddScoped<IUserService, UserService>()`, `AddSingleton<IUploadValidationService,
  UploadValidationService>()`. A caller naming the concrete class would be reaching into a layer it
  is supposed to depend on only by contract, which is the same boundary the
  `Service` → `Repository` rule above draws.
- **An adapter for a system outside the process belongs in `TrailBlaze.Repository`.** EF Core is
  there, and so is `AzureBlobStorageRepository`. It is the layer that already owns reaching
  something external, and a sixth project would introduce a boundary this solution has not
  needed. The contract stays in `TrailBlaze.Interface`, so the vendor type never leaves this
  layer.

### File and code conventions

These are the conventions the existing code follows; match them rather than introducing a
second style in the same solution.

- **File-scoped namespaces**, with the namespace on the first line and `using` directives
  *after* it:

  ```csharp
  namespace TrailBlaze.Service;

  using TrailBlaze.Interface.Service;

  public sealed class OrderService : IOrderService
  { }
  ```

  > **Reconciled 2026-09-16.** Every file in the solution now does this. Until this PR, none
  > did: all 36 were block-scoped, split between `using` above the namespace and `using` inside
  > it, so the solution had two styles and this paragraph matched neither. The exception is
  > `Program.cs`, which has no namespace at all — top-level statements must precede one, and
  > `using` directives must precede the statements, so the file cannot take this form.
  >
  > **The cost of deferring it is the part worth keeping.** Nothing about the fix was hard; it was
  > put off because touching 36 files looks large in review while doing nothing interesting. Feature
  > 01 arrived in the meantime and followed the style it found, so the divergence widened while it
  > waited rather than holding still. A mechanical fix everyone already agrees with is at its
  > cheapest the moment it is agreed to.

- **Generated code keeps the generator's style.** `Migrations/` is emitted by `dotnet ef` with a
  block-scoped namespace, and `TrailBlazeContextModelSnapshot` is rewritten in full on every
  `migrations add`. Reformatting them buys nothing the next regeneration does not undo, so they
  are left as generated. Read the rule above as applying to code this solution authors.

- **Primary constructors** for dependency injection; assign to a `private readonly` field only
  when the parameter is used outside the constructor:

  ```csharp
  public sealed class OrderRepository(TrailBlazeContext context) : DatabaseRepository(context), IOrderRepository
  { }
  ```

- **`sealed`** on concrete classes that are not designed for inheritance. Everything in this
  solution is `sealed` except `EntityBase` (inherited by design) and `DatabaseRepository`
  (inherited by repositories).
- **Nullable reference types are enabled.** A build is expected to be warning-free; fix the
  warning rather than suppressing it to get a green build.

---

## 2. Adding a feature: the vertical slice

Work top-down or bottom-up, but in one pull request, and always through these layers. For an
`Order` feature:

**1. The entity** — `TrailBlaze.Model/DatabaseEntity/Order.cs`

```csharp
namespace TrailBlaze.Model.DatabaseEntity;

public sealed class Order : EntityBase
{
    public required string Reference { get; set; }

    public decimal Total { get; set; }

    public string? Notes { get; set; }
}
```

Derive from `EntityBase` unless the type is genuinely append-only and never soft-deleted
(see `AuditLog` for the one such type, and section 3 for why).

**2. The mapping** — `TrailBlaze.Repository/TrailBlazeContext.cs`

Add a `DbSet` and, if the type needs anything beyond EF's conventions, configure it in
`OnModelCreating`. Indexes and column types belong here, not in attributes on the entity.

```csharp
public DbSet<Order> Orders { get; set; }

// in OnModelCreating
modelBuilder.Entity<Order>(entity =>
{
    entity.HasIndex(order => order.Reference).IsUnique();
});
```

**3. The contract** — `TrailBlaze.Interface/Repository/IOrderRepository.cs`

```csharp
namespace TrailBlaze.Interface.Repository;

using TrailBlaze.Model.DatabaseEntity;

public interface IOrderRepository : IDbRepository;
```

Only add methods beyond `IDbRepository` when the generic operations genuinely cannot express
what you need. **Do not expose `IQueryable`** from a repository — it leaks the data layer into
its callers and makes the query impossible to reason about from the interface.

**4. The implementation** — `TrailBlaze.Repository/OrderRepository.cs`

```csharp
namespace TrailBlaze.Repository;

using TrailBlaze.Interface.Repository;
using TrailBlaze.Model.DatabaseEntity;

public sealed class OrderRepository(TrailBlazeContext context) : DatabaseRepository(context), IOrderRepository
{
    public Task<Order?> GetByReferenceAsync(string reference) =>
        GetAsync<Order>(order => order.Reference == reference);
}
```

**5. Registration** — `TrailBlaze.Repository/PersistenceExtensions.cs`

```csharp
services.AddScoped<IOrderRepository, OrderRepository>();
```

**6. The service contract and implementation** — under `TrailBlaze.Interface/Service/` and
`TrailBlaze.Service/`. Controllers never see a repository; they see a service.

**7. The controller** — `TrailBlaze.Api/Controllers/OrderController.cs`

```csharp
namespace TrailBlaze.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrailBlaze.Interface.Service;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderController(IOrderService orderService) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var order = await orderService.GetAsync(id);
        return order is null ? NotFound() : Ok(order);
    }
}
```

Inherit `ControllerBase`, not `Controller` — `Controller` pulls in view support this project
never uses. **Add `[Authorize]` deliberately**; see section 7, endpoints are anonymous by
default.

**8. Tests** — see section 10. A feature is not finished without one.

---

## 3. Data model rules

### Keys are application-assigned

Every `EntityBase` gets a `string` primary key at construction, in the initializer. This is
not a style preference:

- The row is **identifiable before it is saved**. An insert can be audited with its real id
  (section 5); a caller can return the id it will use without waiting for the database.
- A database-generated key exists only after the save, so any code that needs it — audit,
  logging, an idempotency response — has to run a second query to find out what it wrote.

**Never** reconfigure a key to `ValueGeneratedOnAdd`, and never switch to `int` identity.
Doing so silently breaks audit `EntityId` on insert, and the failure is invisible until
someone needs the history.

Set the id explicitly only when importing or replaying known data; otherwise let the
constructor assign it.

### Soft delete

Deleting sets `IsDeleted`; the row stays. A global query filter, applied **by convention** to
every `EntityBase` type in `TrailBlazeContext.ApplySoftDeleteFilter`, hides those rows from ordinary
reads. Because it is applied by convention rather than entity by entity, a type you add
inherits it automatically — you do not register it, and you cannot forget it.

- Use `IDbRepository.DeleteAsync`, which soft-deletes. Do not call `Remove` on an
  `EntityBase` — that is a hard delete, and the row is gone.
- To read deleted rows, use `.IgnoreQueryFilters()` explicitly. It is deliberately visible at
  the call site: reading history is a decision, not a default.
- A type that should **not** be filtered must not derive from `EntityBase`. `AuditLog` is the
  example: history is append-only, so it has no deleted state to hide.

### Timestamps

`AuditLog.Timestamp` and the four audit columns on `EntityBase` — `CreatedOn`, `CreatedBy`,
`LastModifiedOn`, `LastModifiedBy` — are all `DateTimeOffset`. Record UTC.

**Do not stamp them by hand.** `AuditSaveChangesInterceptor` does it, and it works per entry state
rather than wholesale:

| Entry state | Fields it assigns |
| --- | --- |
| `Added` | `CreatedOn` (unconditional), `CreatedBy` (only where null), `IsDeleted = false` |
| `Modified` | `LastModifiedOn` (unconditional), `LastModifiedBy` (unconditional) |

One consequence of that split is easy to miss and is **not** yet right: nothing assigns
`LastModifiedOn` on insert, so a row that has never been updated stores `default(DateTimeOffset)` —
the year 1 — rather than a real instant. Do not rely on the column being populated; see item
[20](../../docs/tech-debt/20-lastmodified-unset-on-insert.md), which also carries the decision about
which way to fix it.

A value written in a repository method beforehand is therefore **discarded on the same save** — with
`CreatedBy` the one exception, assigned with `??=`, so a caller replaying known data can set it and
keep it. That is the same discretion the key rule above gives to imports.

Writing these by hand is not redundant but **wrong**: it leaves dead code that reads as though it
were load-bearing. That was the defect in
[item 01](../../docs/tech-debt/archive/01-audit-columns-have-two-writers.md), where
`DatabaseRepository.DeleteAsync` wrote `LastModifiedOn` and `LastModifiedBy = "sys"` into a save the
interceptor restamped anyway. Those two assignments are gone and the method now sets only
`IsDeleted`, which is the whole of what a soft delete does. See
section 5 for what `Actor` resolves to and why it is not always the caller.

---

## 4. Persistence rules

- **All mapping configuration lives in `OnModelCreating`.** Indexes, column types, unique
  constraints. Not data annotations on the entity — the entity is a plain model and should not
  carry storage decisions.
- **String columns are `text` by default.** Anything holding JSON must be configured as
  `jsonb` explicitly, as `AuditLog.OldValues` and `NewValues` are. A JSON document stored in
  `text` is a write-only blob; `jsonb` stays queryable.
- **Index what you query by.** Audit history is read by "what happened to this row" and "what
  happened around then", which is why `AuditLog` carries an index on
  `(TableName, EntityId)` and one on `Timestamp`. Add the index when you add the query, not
  after the table is large.
- **The `DbContext` is scoped and is not thread-safe.** Never share one across concurrent
  tasks, and never start a task inside a loop that touches it. See
  [item 02](../../docs/tech-debt/02-deleteasync-n-round-trips.md) of the tech-debt register for the
  batch-write bug in the repository that this rule is aimed at — one `FindAsync` awaited per id,
  with no transaction around the batch.
- **Never put a raw `IQueryable` on a repository interface.**

---

## 5. Auditing

Auditing is automatic. `AuditSaveChangesInterceptor` runs on **both** `SaveChanges()` and
`SaveChangesAsync()` and records every added, modified or deleted entity into `AuditLogs` on
the same context, so history is written in the same transaction as the change it describes.

**You do not write to `AuditLogs` yourself**, and you do not call an audit method. Save the
entity.

### What is recorded

| Field | Value |
| --- | --- |
| `TableName` | The mapped table name. |
| `EntityId` | The primary key, populated for inserts as well as updates (section 3). |
| `Action` | The EF `EntityState`: `"Added"`, `"Modified"`, `"Deleted"`. |
| `OldValues` / `NewValues` | JSON snapshots. `OldValues` is null for an insert, `NewValues` is null for a delete. |
| `ChangedColumns` | Which columns changed, for an update. |
| `Actor` | See below. Never null. |
| `ActorName`, `IpAddress`, `UserAgent`, `CorrelationId` | From the request in flight, when there is one. |

### `Actor` — three distinct values, not one "unknown"

`Actor` is never null, and the fallback is chosen to be informative rather than uniform:

- The Entra **Object ID** when the request is authenticated.
- `"anonymous"` when a request arrived **without** an authenticated user.
- `"system"` when there is **no request at all** — startup, a background job.

A missing token and a background job are different problems. Do not collapse them.

### Opting a property out

Annotate a property with `[NotAudited]` to keep it out of `OldValues` / `NewValues`:

```csharp
[NotAudited]
public string? PasswordHash { get; set; }
```

Audit rows typically have looser access control than the tables they describe, so a value
copied into one becomes readable by people who cannot read the source table — and it stays
readable for as long as the history is kept. **Annotate a property the moment it becomes
sensitive** — a credential, a token, a government identifier, personal data — rather than
waiting for a leak to point it out. Nothing in this solution is annotated yet, because the
only entity holds no secret.

---

## 6. Configuration

Configuration is `IConfiguration`: `appsettings.json`, then `appsettings.{Environment}.json`,
then user-secrets, then environment variables, then command line. Later sources win.

**Rules:**

- **Read configuration only at the composition root** (`Program.cs`) and pass *resolved
  values* down. Library layers take a `string? connectionString`, not an `IConfiguration`.
  This is what keeps `TrailBlaze.Repository` config-agnostic and testable.
- **No static configuration class.** An earlier design read the process environment in a
  static constructor and threw from it, which coupled unrelated settings: a missing `TenantId`
  broke code that only wanted the connection string. Do not reintroduce it.
- **Keys are flat and unprefixed**, so the same name works as an environment variable:
  `DbConnection=...` on the command line, `DbConnection` in `appsettings.json`.
- **Every key is declared in `appsettings.json` with an empty default.** An empty value means
  "not configured", which is a defined state. Add the key name to `Constant.ConfigKey` so it is
  never spelled as a literal in two places.
- **A key the application cannot run without is enforced at the composition root**, by
  `RequireSetting`, which fails startup with a message naming the key. Whether "not configured"
  is a tolerable state is a per-key decision, and this is where it is made: `TenantId` and
  `Audience` are allowed to be absent, while `DbConnection`, `BlobConnection` and `AllowedOrigins`
  are not. Declaring a key empty and requiring it are not in conflict — the file states the key
  exists, and the root states what a missing value means. Requiring a key is for a setting the
  application cannot work without, not for one whose absence an operator should notice: feature 03's
  `AdminObjectId`/`AdminDisplayName` were both required, and both were removed on 2026-09-19 along
  with the startup seeder that read them, because the administrator is a database row a person edits
  rather than a configuration fact the host enforces.
- **Credentials never go in the repository.** Not in `appsettings.json`, and never in
  `Properties/launchSettings.json` — that file is version-controlled, so anything in it is
  handed to every clone. Use user-secrets:

  ```bash
  dotnet user-secrets set "DbConnection" "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=TrailBlaze;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False"
  ```

- **Every deployed environment runs against the real Azure SQL Database, not a local stand-in.**
  Azure SQL Database is managed and has no image to run, and the API is deployed to Azure
  Container Apps from `src/api/Dockerfile`, so the SQL Server's firewall must allow the caller.
  There is no `docker-compose.yml` and none is wanted — neither deployment target consumes a
  local multi-service stack, so a compose file claiming to stand in for one would be fiction.
  (`docker-compose.test.yml` is not that: it starts no application process, serves only the test
  tier, and §10 covers it.)

  **Scoped 2026-09-19: `dotnet run` may point at a SQL Edge container on loopback.** A developer's
  user-secret `DbConnection` names `Server=127.0.0.1,1433;Database=TrailBlaze`, which is a
  container started by hand and migrated by the developer. Nothing else reads it: deployed
  environments take the pipeline's `DbConnection`, which names Azure SQL, and no container is
  reachable from ACA — so this is an exception with a boundary, not a rule that moved.

  **The container is not the test tier's, and the two contend for the same port.** Both bind
  `127.0.0.1:1433`, so only one can be up, and while the dev container holds the port a
  `dotnet test` run reaches *it* — which is safe only because the tier creates and drops a
  database per collection and never touches `TrailBlaze`. Stop the dev container to get the
  isolated stack back. Do not point the test tier at the dev database deliberately: it drops
  what it creates, and a schema in use is exactly what it has no reason to preserve.

  **What the exception costs** is that Azure SQL Edge is not Azure SQL Database. A behaviour
  depending on the managed engine's version, collation, or certificate is no longer exercised
  before deployment, so "it works locally" is evidence about the container and nothing else —
  §10's suite, which runs against a real engine, remains the thing that speaks to engine shape.

- **A deployed environment's connection string carries `Encrypt=True` without
  `TrustServerCertificate=True`**, since a managed Azure SQL server presents a real certificate
  and there is nothing self-signed to accept. **Two exceptions, both narrow, and both on
  loopback:** the test tier, and the local-dev container above. Azure SQL Edge serves a
  self-signed certificate while `Microsoft.Data.SqlClient` 6.x encrypts by default, so a string
  aimed at the container must carry `TrustServerCertificate=True` or `Encrypt=Optional`. That
  keyword never appears in `appsettings.json`, and the one user-secret that carries it names
  `127.0.0.1` — an exception that also reaches a real server would be the bug this rule exists to
  prevent, so the host is part of the exception and not incidental to it.

  The test tier's half is enforced rather than merely written down —
  `TrailBlaze.Repository.Test`'s `TestEnvironment` parses a `TRAILBLAZE_SQL_CONNECTION` pointing
  at a loopback host and refuses a string that lacks the keyword, because the failure it prevents
  is a certificate error that names neither the certificate nor the missing keyword. **The
  local-dev half is not enforced, and that is a gap rather than a decision**: nothing stops a
  user-secret naming a real server from carrying the keyword, and the check that would — refuse
  `TrustServerCertificate=True` on a non-loopback `DbConnection` at the composition root — is not
  written. Recorded here so the asymmetry between the two halves is visible.

- **A missing required setting fails at startup with a message naming the setting** — not with
  a null reference when the first request arrives, and not with an exception naming a local
  variable.

---

## 7. Authentication and authorization

Authentication is Entra ID bearer tokens, wired in `ServiceExt.AddEntraAuthentication`. It
activates only when **both** `TenantId` and `Audience` are configured:

- Both present — JWT bearer is the default scheme, validating issuer, audience, lifetime and
  signing key against `https://login.microsoftonline.com/{tenantId}/v2.0`.
- Either missing — **no authentication scheme is registered**, and the host logs a warning at
  startup saying every request is anonymous. The application still starts, so `/health` and
  OpenAPI work on a project that has not been pointed at a tenant yet.

There is **no client secret**. Validating an inbound token needs only the public signing keys
that the authority serves. Do not add one.

### The rule that matters

> **The scheme validates tokens. It does not protect endpoints.**

**Every endpoint is anonymous until you put `[Authorize]` on it** — including with `TenantId`
and `Audience` both set. Put it on the controller by default and open individual actions
deliberately with `[AllowAnonymous]`, so the exception is visible in the diff:

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrderController(IOrderService orderService) : ControllerBase
{
    [HttpGet("health")]      // the one endpoint that is deliberately public
    [AllowAnonymous]
    public IActionResult Health() => Ok();
}
```

Read the authenticated caller from `IUserContextService` (section 8), never by parsing the
token again in your own code.

---

## 8. Knowing who is acting

Inject `IUserContextService` (`TrailBlaze.Interface/Infrastructure/`). It exposes `EntraObjectId`,
`HasActiveRequest`, `ActorName`, `IpAddress`, `UserAgent` and `CorrelationId`, all read-only.

Every member is get-only by design: the values describe the request in flight, and nothing
outside the abstraction should be able to rewrite them. Treat `null` as a real answer — there
may be no request at all — and branch on `HasActiveRequest` when the difference matters, as
`Actor` resolution does in section 5.

---

## 9. HTTP behaviour

### Errors

`AddProblemDetails()` and `UseExceptionHandler()` are registered, so an unhandled exception
becomes an RFC 9457 `application/problem+json` response with a traceable id. **Let exceptions
propagate** — do not add a catch-all that rewrites every failure into a `500` or, worse, a
`401`. Throw a specific exception, return a correct status code for expected outcomes
(`NotFound()`, `BadRequest()`), and let the handler deal with the unexpected.

Do not put stack traces or exception messages in a response body outside Development.

### Health and API description

- `GET /health` — liveness. It answers without a token, and it does not touch the database, so
  it stays correct while the database is unreachable. It is **not** a way to run the app with no
  database configured: `DbConnection` is required and the host refuses to start without it
  (§6). Liveness answers "is this process up", not "can it serve every route".
  A green `/health` also does not mean the instance has an administrator. Nothing in the application
  grants the role — the admin is one row whose `Role` column someone set by hand — so an instance
  that is up, liveness-green and correct in every other respect simply has no admin in it until
  that statement is run. That is a deliberate trade rather than a gap; see
  [03-admin-seeding.md § Decisions](../../docs/features/archive/03-admin-seeding.md#decisions).
- `/openapi/v1.json` — the OpenAPI document, Development only.

### CORS

`AllowedOrigins` is a **comma**-separated list; `ServiceExt.AllowCORS` splits on `,` and
trims. A semicolon produces one origin that matches nothing, and the failure appears in a
browser, far from the setting. When it is empty the application **fails at startup** with a
message naming `AllowedOrigins` — a silent misconfiguration here is a production incident.

Development defaults are set in `appsettings.Development.json` and must stay in step with the
`applicationUrl` values in `launchSettings.json`.

### Pipeline order

`Program.cs` registers middleware in a deliberate order, and it matters:

```
UseExceptionHandler      wraps everything downstream
  MapOpenApi             Development only
  UseHttpsRedirection
  UseCors                before authentication: preflight requests are unauthenticated
  UseAuthentication      must precede UseAuthorization
  UseAuthorization
  MapControllers
  UseHealthChecks
```

**Do not insert middleware without deciding where it belongs in this list.** Anything
registered after `MapControllers()` still runs before the endpoints, because `UseRouting` and
`UseEndpoints` are positioned automatically — so "it looked like it went last" is not a reason
to put it there.

---

## 10. Testing

Tests live in the per-layer `*.Test` projects (section 1) and run with `dotnet test`. **A behaviour
change without a test is not finished**; where tests exist in this solution, they exist because
each one catches a specific regression that had already happened once.

> **State as of 2026-09-19:** `TrailBlaze.Repository.Test` runs against two containers —
> `azure-sql-edge` and `azure-storage-edge`, started by `docker-compose.test.yml` — and every test
> that needs one is tagged `Category=Container` and **skips** when it cannot reach it.
> `TrailBlaze.Api.Test` stays offline. `TrailBlaze.Service.Test` holds tests again as of feature 03,
> and they are offline too: `RoleComesFromTheRowTests`, two reflection assertions that a role has no
> source but the row. The project has no container test and no reference to another test project —
> the one it briefly had, for a service deleted in review, went with it
> ([tech-debt 25](../../docs/tech-debt/25-service-test-tier-is-empty.md) still asks where a
> store-backed service assertion runs, and feature 03 turned out not to need the answer).

### Two tiers, and which one runs when

| Tier | Needs | Consequence |
| --- | --- | --- |
| **Offline** | nothing | always runs |
| **`Category=Container`** | `azure-sql-edge`, `azure-storage-edge` | skips — never fails — when unreachable |

```bash
docker compose -f docker-compose.test.yml up -d    # from src/api; reads MSSQL_SA_PASSWORD from .env
dotnet test                                        # everything runnable
dotnet test --filter "Category=Container"          # the container tier alone
dotnet test --filter "Category!=Container"         # the tests touching neither container
```

**`Category=Container` is the only trait in the solution, and the two filters are not
complements.** `Category!=Container` selects the tests that touch *neither* container — it is not
"the offline run", because it also drops the storage tier, which runs whenever a storage endpoint
answers, configured or not. The storage tests run against the emulator fallback (storage needs no
secret by design), so a run with nothing configured passes the storage tier and the offline tier and
**skips the database tier and only it**. Measured, not derived.

**What that run prints is written down in one place — [testing-and-tdd.md](../../docs/testing-and-tdd.md) —
and no count belongs here.** A number restated in four documents goes stale in four, and it did:
it moved 31 → 42 → 59 → 76 in three days, each move a hand edit
([item 19](../../docs/tech-debt/19-doc-indexes-drifted.md)). This section keeps the claim, which is
what a reader needs; the strategy doc keeps the measurement, which is what has to be re-run.

### The database tier

**`Migrate()`, never `EnsureCreated()`.** `EnsureCreated` builds the schema from the model and
bypasses `Migrations/` entirely, so a broken migration stays broken and the test still passes — a
test that cannot fail. It also forecloses `Migrate()` permanently: an `EnsureCreated` database has
no `__EFMigrationsHistory`, so a later `Migrate()` tries to create tables that already exist.

Each test class gets **one database of its own**, created and migrated in a fixture, dropped on
dispose. Not one shared database, and not a transaction rolled back per test: a read-back through
the *same* context hits EF's change tracker rather than the database, and a genuine second context
needs a second connection in the same transaction, which needs MSDTC — absent from a Linux SQL Edge
container. Row-level cleanup was the other candidate and is worse: this repo has no hard delete
(`IDbRepository.DeleteAsync` is a *soft* delete), so it would mean raw SQL ordered by foreign key,
extended by every future feature, rotting silently.

A test that must build its own schema — `MigrationNarrowingTests` drives the schema to an earlier
migration by hand — gets a database per *test* through `TrailBlazeServerFixture`, because a
database cannot be returned to an earlier migration once anything has taken it to head.

**Where the skip boundary is.** Not reaching the configured server skips; the container is simply
not running. Everything past that is a failure, deliberately — a migration that will not apply
against a real engine is the exact bug this tier exists to find, and degrading it to a skip would
hide the one thing it was built for.

`FakeUserContext` stands in for the HTTP-backed implementation; set the caller you want to test —
authenticated, anonymous, or no request at all.

### `Skip.IfNot` works by throwing, and that trips three things

```csharp
[SkippableFact]                                  // NOT [Fact]
public async Task A_soft_deleted_row_disappears()
{
    Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
    ...
}
```

1. **Under a plain `[Fact]` it fails the test.** `Skip.IfNot` throws `SkipException`, and only
   `[SkippableFact]`/`[SkippableTheory]`'s runner converts that into a skip. Under `[Fact]` it is
   an exception like any other — twelve tests went red with nothing configured before this was
   understood.
2. **Inside anything that catches, the skip is swallowed.** `Record.ExceptionAsync` and
   `Assert.ThrowsAsync` catch everything, including the `SkipException`, which then comes back as
   the exception under test; the test fails reporting the skip message as its *expected* value.
   Resolve the fixture's client or repository **before** the recorder, never inside it.
3. **Only the fixtures skip for you.** Branching on `IsAvailable` by hand is how a test ends up
   either not skipping or skipping for a reason it never states.

### What the container tier cost: one lost claim, and one that got stronger

The old offline repository tier pointed EF at an unreachable port and asserted every save
*failed*. It worked because save interception runs before a connection opens, so the staged audit
rows were readable with no database at all.

- **Lost, and not worth mourning.** That harness proved *EF runs interception before it opens a
  connection*. That is an EF ordering guarantee, not a TrailBlaze behaviour, and it is unobservable
  against a live server. Nothing asserts it now, and nothing should.
- **Stronger, not merely equivalent.** The old tier read the audit rows out of the change tracker
  that had just written them, so "the row was recorded" meant "the interceptor staged something".
  The tier reads them back through a fresh scope, so every assertion is a round trip: SQL Server
  accepted the write and the row is on disk.
- **The tripwire is gone, and that is the real loss.** The old harness asserted that the save
  *failed*, as a signal that the tier had not silently stopped testing. That job is now done by
  the skip — which is louder, and has its own failure mode, described above.

Queries can be inspected without executing them using `ToQueryString()`, which builds the SQL and
stops. `SoftDeleteFilterTests` still does exactly that; `SoftDeleteExecutionTests` is the container
tier that complements it, because a generated predicate and an applied one are different claims.

### Rules

- **Test behaviour, not implementation.** Assert on what a caller observes.
- **A test that cannot fail is not a test.** For anything asserting on generated SQL or on
  absence, comment out the thing under test and confirm the test goes red before you trust it.
  A substring check on a column name passes whether or not the filter is applied; assert on
  the `WHERE` clause, or pair it with a control test that asserts the opposite.
- **No behaviour to assert? Assert the invariant instead.** Some correct changes have no runtime
  behaviour to drive — correcting a document, removing a dead member, deduplicating a configuration
  value. When the thing you are protecting is a durable property of a **file** ("every command is
  listed in the README", "no permission entry names another repository"), write a test that reads the
  file and asserts the property, and confirm it goes red when the file is reverted. This is a
  **`doc-assertion`**: a real test with a real failure mode, and the one mechanism that would have
  caught a standard asserting a property of the code that the code had stopped having.
- **When nothing at all can be asserted, say so instead of inventing coverage.** A workflow file, a
  deployment step, a request line in a `.http` file — some work has no invariant a test could hold.
  Do not write a test that cannot fail to make the change look finished. Record what you ran and what
  you observed in a `Verification:` line, and state in the pull request why there is no test. **A
  verified claim and a tested claim are different strengths of claim**, and blurring them is worse
  than either; the tech-debt register marks its items with exactly this three-way split.
- **Name the condition and the expected result**: `An_unauthenticated_request_is_recorded_as_anonymous`,
  not `TestActor2`.
- **One behaviour per test.** Use `[Theory]` for the same behaviour across inputs.

---

## 11. Continuous integration

> **State as of 2026-09-15:** this repository has no CI workflow — there is no `.github/`
> directory, so neither job below runs. The section describes the target, and wiring it is part of
> the foundation work. The **second** job belongs to the upstream template repository this
> solution was generated from: there is no `SampleTemplate/` and no template package here to pack,
> so only the first job is in scope.

Two jobs run on every push and pull request.

**Build and test** — builds `TrailBlaze.slnx` and runs the suite.

**Generate from the template and build the result** — packs the template package, installs it
into an isolated template hive, generates a project, then builds **and tests what came out**,
and fails if any file still carries the template's placeholder name.

The second job exists because the first cannot see template rot. Files under the template are
only ever exercised *after* `dotnet new` has rewritten project names in both paths and file
contents, so a stale path, a wrong base image, or a name the rewriter misses builds perfectly
in place and breaks for every user. **Any change to the template's structure, file names, or
project names must keep that job green.**

---

## 12. Where the code and this standard do not yet agree

**This list now lives in [docs/tech-debt/](../../docs/tech-debt/00-debt-log.md).**

It moved because it had drifted, and the drift is worth recording rather than quietly repairing.
Item 1 below spent months asserting that the audit columns were unmaintained — after
`AuditSaveChangesInterceptor` had started maintaining them. A document whose whole purpose is to be
trustworthy about exactly that was wrong about it, and nothing noticed, because nothing checked.
The register's first act was to re-verify every item against the code before filing it, and the same
false claim was found in [PRD.md](../../docs/PRD.md) line 160.

**File debt there, not here.** Not in a feature file, and not as a passing note in a pull request
either. A claim recorded in two places is a claim that will disagree with itself; that is the defect
this move cures, and re-creating a second list here would restore it.

Numbers **01–12** in the register are the former items of this section, in the same order, and they
never change — so a reference to "§12.N" written before the move still resolves.

| Was | Now | State |
|---|---|---|
| 12.1 Audit columns are not maintained | [01 — Audit columns had two writers](../../docs/tech-debt/archive/01-audit-columns-have-two-writers.md) | archived — a verified close, PR #8; the claim was false — see below |
| 12.2 `DeleteAsync` issues one `FindAsync` per id | [02 — `DeleteAsync` does N round trips, untransacted](../../docs/tech-debt/02-deleteasync-n-round-trips.md) | open |
| 12.3 Migrations were Npgsql-shaped | [03 — Migrations were Npgsql-shaped](../../docs/tech-debt/archive/03-migrations-npgsql-shaped.md) | archived — feature 01, PR #3 |
| 12.4 `UserController` diverges from the route convention | [04 — `UserController` violates the route convention](../../docs/tech-debt/04-usercontroller-route-convention.md) | open |
| 12.5 A soft delete is recorded as `"Modified"` | [05 — A soft delete is recorded as `"Modified"`](../../docs/tech-debt/05-soft-delete-recorded-as-modified.md) | open |
| 12.6 Timestamps are inconsistent | [06 — Timestamp types were inconsistent](../../docs/tech-debt/archive/06-timestamp-types-inconsistent.md) | archived — a facet of 01, no change of its own |
| 12.7 `TrailBlaze.Api.http` requests `/weatherforecast/` | [07 — The `.http` file requests `/weatherforecast/`](../../docs/tech-debt/07-http-file-requests-weatherforecast.md) | open |
| 12.8 `SampleTemplate/placeholder.txt` | [08 — `SampleTemplate/placeholder.txt`](../../docs/tech-debt/archive/08-sampletemplate-placeholder.md) | archived — the template is not here |
| 12.9 `DbConnection` is accepted empty | [09 — `DbConnection` was accepted empty](../../docs/tech-debt/archive/09-dbconnection-accepted-empty.md) | archived — feature 01, PR #3 |
| 12.10 Namespaces are block-scoped | [10 — Namespaces were block-scoped](../../docs/tech-debt/archive/10-block-scoped-namespaces.md) | archived — 2026-09-16 |
| 12.11 §11 describes a CI workflow that was not built | [11 — There is no CI or deployment pipeline](../../docs/tech-debt/11-no-ci-pipeline.md) | open |
| 12.12 Feature 02's tests were deferred | [12 — Feature 02's behaviour shipped without the tests §10 requires](../../docs/tech-debt/12-feature-02-tests-deferred.md) | open — narrowed by feature 03, which wrote the `Role` reflection guard |

### Two things the old list got wrong

Both are the same failure — a claim about the code that no longer matched the code — and both were
found by re-checking rather than by reading. They are recorded here because a reader who remembers
the old wording should know which half of it to discard.

- **12.1 was false, and the truth is narrower.** The columns are maintained — `AuditTests` proves it
  — though not uniformly: the interceptor stamps `CreatedOn`/`CreatedBy` on `Added` and
  `LastModifiedOn`/`LastModifiedBy` on `Modified`, so a row that has never been updated keeps its
  sentinel `LastModifiedOn` outright (item
  [20](../../docs/tech-debt/20-lastmodified-unset-on-insert.md)). What was actually wrong was that
  there were **two writers** — `DatabaseRepository.DeleteAsync` set `LastModifiedOn` and
  `LastModifiedBy = "sys"`, and the interceptor overwrote both on the same save, so the repository's
  writes were dead. Not "unmaintained columns" but "a dead writer", which is
  [item 01](../../docs/tech-debt/archive/01-audit-columns-have-two-writers.md), and that dead writer
  is now deleted.
- **12.6's type claim was right and §3's was wrong.** The columns are `DateTimeOffset`; §3 called
  them `DateTime` and added a second claim that they were not yet maintained. §3 is corrected in the
  same pull request as this pointer.

Four items (12.3, 12.8, 12.9, 12.10) had already resolved before the move, and two more (12.1, 12.6)
have resolved since; all six carry their history in `archive/`. Item 12.10 in particular is kept
rather than deleted: it is the clearest example in this
repository of a divergence that stayed open because it was deferred as mechanical — and it widened
meanwhile, because feature 01's new files copied the style that was there.


---

## 13. Pull request checklist

- [ ] New code respects the dependency direction in section 1.
- [ ] New entities derive from `EntityBase`, or the reason they do not is stated.
- [ ] Keys are application-assigned; no key was reconfigured to be database-generated.
- [ ] Anything soft-deleted goes through `DeleteAsync`, not `Remove`.
- [ ] Sensitive properties carry `[NotAudited]`.
- [ ] New configuration keys are flat, declared empty in `appsettings.json`, and named via
      `Constant.ConfigKey`. No credential is committed.
- [ ] New endpoints carry `[Authorize]`, or the reason they do not is stated.
- [ ] Tests cover the behaviour, and any test asserting on generated SQL was confirmed to fail
      when the thing under test is removed.
- [ ] `dotnet build` and `dotnet test` pass, and CI's template job stays green.
- [ ] Any rule above that had to be worked around is updated in this document, in the same
      pull request.
