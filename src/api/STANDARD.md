# Backend API Development Standard

This document is the standard for backend APIs built on this solution. It is shipped inside
the project, so it travels with the code: cite it in review, and if you find yourself working
around a rule here, change the rule in a pull request rather than in one service.

It is prescriptive. Where a rule exists because of a specific past failure, the reason is
given — not to justify the rule, but because a rule whose reasoning is missing gets
"simplified" away by the next person.

**Reading it:** sections 1–2 are the layout and the one workflow you will use most. Sections
3–9 are the rules by area. Section 10 is testing, 11 is CI, and **12 lists the places this
standard and the code do not yet agree** — read that one before trusting the rest blindly.

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
  access, and it sits beside `IDbRepository` and `IUserRepository`. The folder follows the kind
  of the contract, not the suffix on its name.
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

  > **State as of 2026-09-15:** no file in this solution actually does this — every one uses the
  > block-scoped form, with `using` directives above the namespace. New code follows the codebase
  > rather than this paragraph, because a rule that half the files break is worse than a rule that
  > is wrong: the point is not having two styles. Reconciling them is a mechanical PR of its own,
  > and until it lands, **match the file you are editing**. See §12.

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

`AuditLog.Timestamp` is a `DateTimeOffset`. The audit columns on `EntityBase`
(`CreatedOn`, `LastModifiedOn`) are `DateTime` and are **not yet maintained** — see section
12. Record UTC, and prefer `DateTimeOffset` for anything new.

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
  tasks, and never start a task inside a loop that touches it. See section 12 for a
  batch-write bug of exactly this shape found in the repository.
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
  `Audience` are allowed to be absent, while `DbConnection`, `BlobConnection` and
  `AllowedOrigins` are not. Declaring a key empty and requiring it are not in conflict — the
  file states the key exists, and the root states what a missing value means.
- **Credentials never go in the repository.** Not in `appsettings.json`, and never in
  `Properties/launchSettings.json` — that file is version-controlled, so anything in it is
  handed to every clone. Use user-secrets:

  ```bash
  dotnet user-secrets set "DbConnection" "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=TrailBlaze;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False"
  ```

- **Every environment uses the real Azure SQL Database, not a local stand-in.** Azure SQL Database
  is managed and has no image to run, so there is no `docker-compose.yml` and no database
  container: local development is `dotnet run` against the real server, and the API is deployed to
  Azure Container Apps from `src/api/Dockerfile`. Two things follow — the SQL Server's firewall
  must allow the caller, and the connection string carries `Encrypt=True` without
  `TrustServerCertificate=True`, since there is no self-signed certificate to accept.

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

> **State as of 2026-09-15:** the harness exists. The three test projects reference the layer they
> exercise and `dotnet test` discovers tests in each. `TestSupport/AuditHarness.cs` and
> `FakeUserContext` are in `TrailBlaze.Repository.Test`; `TestSupport/FakeStorageRepository.cs` is in
> `TrailBlaze.Service.Test`. The pattern below is what they implement.

### The pattern: no database required

EF Core runs save interception **before it opens a connection**. Point the context at a port
nothing listens on, and the audit entries are already staged in the change tracker by the time
the save fails — so audit behaviour is testable with no database at all. `TestSupport/AuditHarness.cs`
is the helper that wraps this — **to be added in feature 01** — and it wires the context through
`AddRepositoryPersistence` rather than by hand, so tests exercise the same composition the
application uses.

```csharp
[Fact]
public void An_insert_is_recorded_with_its_key_and_table()
{
    using var harness = new AuditHarness();
    var order = new Order { Reference = "A-1" };
    harness.Context.Orders.Add(order);

    harness.Save();                       // fails on connection, after interception

    var log = harness.SingleEntry();
    Assert.Equal("Orders", log.TableName);
    Assert.Equal(order.Id, log.EntityId);
}
```

`FakeUserContext` stands in for the HTTP-backed implementation; set the caller you want to
test — authenticated, anonymous, or no request at all.

Queries can be inspected without executing them using `ToQueryString()`, which builds the SQL
and stops.

### Rules

- **Test behaviour, not implementation.** Assert on what a caller observes.
- **A test that cannot fail is not a test.** For anything asserting on generated SQL or on
  absence, comment out the thing under test and confirm the test goes red before you trust it.
  A substring check on a column name passes whether or not the filter is applied; assert on
  the `WHERE` clause, or pair it with a control test that asserts the opposite.
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

Listed so you are not surprised by them, and so fixing one is an obvious pull request.

1. **Audit columns are not maintained.** `DatabaseRepository.CreateAsync` does not set
   `CreatedOn` or `CreatedBy`, and `UpdateAsync` does not set `LastModifiedOn` or
   `LastModifiedBy`. Only `DeleteAsync` sets them — and it hardcodes `LastModifiedBy = "sys"`
   instead of reading `IUserContextService`, so it attributes a real user's action to the
   system even though the audit trail knows who they were. Until this is fixed, `CreatedOn` is
   `default(DateTime)` and `LastModified*` cannot be trusted.
2. **`DeleteAsync` issues one `FindAsync` per id** and wraps the batch in no transaction, so a
   large delete is N round trips and can partially apply.
3. **Migrations exist but are Npgsql-shaped — resolved, and now historical.** The provider swap
   (feature 01) replaced `Npgsql` with `Microsoft.EntityFrameworkCore.SqlServer` and regenerated
   the migration set, so `TrailBlaze.Repository/Migrations` is SQL Server-shaped, as is the
   `nvarchar(max)` mapping on the audit snapshots. The strategy question this item used to raise
   is settled: migrations are applied by the **deployment pipeline** with
   `dotnet ef database update`, before the new revision takes traffic. An `IHostedService` that
   migrated at startup was built and then removed — Azure Container Apps runs several replicas and
   concurrent startup migrations race over the same DDL. `TrailBlazeContextFactory` therefore
   resolves `DbConnection` from the environment and throws when it is absent, rather than
   defaulting to a local string that would migrate the wrong database.
4. **`UserController` diverges from section 2's route convention.** It inherits `Controller` rather
   than `ControllerBase`, routes on `[controller]` rather than `api/[controller]`, and its `index`
   action is a placeholder returning a bare string. `UserService` is no longer empty —
   `GetOrCreateAsync` implements first-sight provisioning — so the scaffolding to replace here is
   the controller, not the service.
5. **A soft delete is recorded as `"Modified"`,** not as a distinct `"Deleted"`, because
   `Action` holds the EF `EntityState`. A soft delete is therefore indistinguishable in the
   history from an ordinary update.
6. **Timestamps are inconsistent.** `EntityBase` and `AuditLog` both use `DateTimeOffset` now, but
   `DatabaseRepository` writes `DateTime.UtcNow` into them.
7. **`TrailBlaze.Api.http` requests `/weatherforecast/`,** which does not exist in this solution.
8. **`SampleTemplate/placeholder.txt` — resolved, and now historical.** That file belonged to the
   upstream template this solution was generated from, and neither it nor the template lives here
   any more. The template CI job described in section 11 referred to it; this repository has no CI
   at all.
9. **`DbConnection` is accepted empty — resolved, and now historical.** Feature 01 introduced
   `RequireSetting`, and the composition root now requires `DbConnection`, `BlobConnection` and
   `AllowedOrigins`, failing startup with a message naming the key that is missing. The two cases
   §9 previously left open — "runs for `/health` without a database" — no longer hold; see the
   corrected note there.
10. **Namespaces are block-scoped, not file-scoped.** §1 prescribes file-scoped namespaces; every
    file in the solution uses the block-scoped form. Feature 01's new files followed the code, so
    the divergence is now wider, not narrower. It is mechanical and worth its own PR — see the
    note in §1.
11. **§11 describes a CI workflow that feature 01 deliberately did not build.** The foundation
    feature lists "no CI pipeline definition" among its non-goals, so §11 remains a description of
    the target rather than of anything wired up. Saying it is "part of the foundation work" was
    wrong; it is not claimed by any feature yet. It has grown more urgent since: the API is
    deployed to Azure Container Apps and the web app to Azure Static Web Apps by GitHub workflow,
    so a pipeline is now the only path either has to production — it is still not written.

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
