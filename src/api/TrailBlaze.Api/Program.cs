using TrailBlaze.Api.Extension;
using TrailBlaze.Model;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Turns unhandled exceptions into RFC 9457 problem+json. This is where error handling
// belongs -- not a catch-all that rewrites every failure into a 401.
builder.Services.AddProblemDetails();

// Register dependence. Configuration is read here, at the composition root: IConfiguration
// layers environment variables over appsettings.json, so the flat keys below keep their
// existing env var names and deployments need no changes.
//
// The two connection strings are required rather than optional, so a missing one stops the host
// at startup naming the key instead of surfacing as a null reference on the first request that
// happens to need it. They are read here and passed to RegistService, which does the
// registering -- everything else that gets registered, including Entra and CORS, is registered
// in there too, so this file composes nothing on its own.
string dbConnection = builder.Configuration.RequireSetting(
    Constant.ConfigKey.DBCon, Constant.Message.NoDbConnection);
string blobConnection = builder.Configuration.RequireSetting(
    Constant.ConfigKey.BlobConnection, Constant.Message.NoBlobConnection);

builder.Services.RegistService(builder.Configuration, dbConnection, blobConnection);

// Migrations are deliberately NOT applied here. Azure Container Apps runs several replicas, and
// replicas migrating concurrently on startup race each other over the same DDL -- one wins, the
// other gets "there is already an object named ..." or deadlocks, intermittently and only at
// deploy time. The deployment pipeline applies them with `dotnet ef database update` before the
// new revision takes traffic, so exactly one writer ever touches the schema.

var app = builder.Build();

// Say it out loud rather than letting the app look protected when it is not: with no
// tenant and audience there is no authentication scheme, and every request is anonymous.
if (string.IsNullOrWhiteSpace(builder.Configuration[Constant.ConfigKey.TenantId])
    || string.IsNullOrWhiteSpace(builder.Configuration[Constant.ConfigKey.Audience]))
{
    app.Logger.LogWarning(
        "Entra ID authentication is not configured, so every request is anonymous. "
        + "Set the {TenantId} and {Audience} configuration values to enable it.",
        Constant.ConfigKey.TenantId,
        Constant.ConfigKey.Audience);
}

// Configure the HTTP request pipeline.

// First, so it wraps everything downstream.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(Constant.App.CORSPolicyName);

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.UseHealthChecks(Constant.App.HealthCheckUrl);

app.Run();
