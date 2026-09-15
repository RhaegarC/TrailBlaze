using TrailBlaze.Api;
using TrailBlaze.Interface.Infrastructure;
using TrailBlaze.Interface.Service;
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
builder.Services.RegistService(builder.Configuration[Constant.ConfigKey.DBCon]);
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddEntraAuthentication(builder.Configuration);
builder.Services.AllowCORS(builder.Configuration);
builder.Services.AddHealthChecks();

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
