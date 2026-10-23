using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using VibeZone.Api.Web;
using VibeZone.Infrastructure.Email;
using VibeZone.Infrastructure.Health;
using VibeZone.Infrastructure.Observability;
using VibeZone.Infrastructure.Outbox;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Infrastructure.Security;
using VibeZone.Infrastructure.Storage;
using VibeZone.Infrastructure.Web;
using VibeZone.Migrations;
using VibeZone.SharedKernel;

var builder = WebApplication.CreateBuilder(args);

// Build-time OpenAPI generation (GetDocument.Insider) boots the host without real configuration;
// give it inert values so fail-fast validation doesn't break the build. Nothing is contacted.
var generatingOpenApiDocument = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (generatingOpenApiDocument)
{
    builder.Configuration["ConnectionStrings:Database"] = "Host=openapi-generation.invalid";
    builder.Configuration["Email:From"] = "openapi@generation.invalid";
    builder.Configuration["Email:SmtpHost"] = "openapi-generation.invalid";
    builder.Configuration["App:PublicBaseUrl"] = "https://openapi-generation.invalid";
    builder.Configuration["Storage:Bucket"] = "openapi-generation";
    builder.Configuration["Storage:AccessKey"] = "openapi-generation";
    builder.Configuration["Storage:SecretKey"] = "openapi-generation";
}

builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);
builder.AddVibeZoneLogging();
builder.AddVibeZoneTelemetry("vibezone-api");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

var modules = AppModules.Create();
builder.Services.AddSingleton(modules);
builder.Services.AddVibeZonePersistence(builder.Configuration);
builder.Services.AddVibeZoneEmail(builder.Configuration);
builder.Services.AddVibeZoneStorage(builder.Configuration);
builder.Services.AddOptions<PublicUrlOptions>().Bind(builder.Configuration.GetSection(PublicUrlOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
// Keys protect auth cookies, antiforgery and Identity tokens; shared through Postgres so they survive
// restarts and work across instances. Encryption at rest of the key ring is a production task (M5).
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("VibeZone");
if (!generatingOpenApiDocument)
{
    dataProtection.PersistKeysToDbContext<AppDbContext>();
}

modules.AddServices(builder.Services, builder.Configuration);
if (!generatingOpenApiDocument)
{
    // Integration events are dispatched by the API (not the worker): notification handlers push over SignalR (ADR-0012).
    builder.Services.AddOutboxDispatcher();
}

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi("v1");
var cookieSecurity = CookieSecurity.From(builder.Configuration);
builder.Services.AddVibeZoneAntiforgery(cookieSecurity);
builder.Services.AddVibeZoneRateLimiting(builder.Configuration);
builder.Services.AddVibeZoneForwardedHeaders(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag], timeout: TimeSpan.FromSeconds(5))
    .AddStorageCheck(HealthEndpoints.ReadyTag);
// CORS is intentionally not registered: browsers reach the API through the web origin (ADR-0002).

var app = builder.Build();

if (args.Contains(VibeZone.Api.DevSeed.Command))
{
    return await VibeZone.Api.DevSeed.RunAsync(app);
}

if (!cookieSecurity.Secure)
{
    app.Logger.LogWarning("{Setting}=false: session and CSRF cookies are not Secure. Only acceptable for plain-HTTP local development.", CookieSecurity.ConfigurationKey);
}

app.UseVibeZoneForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseSerilogRequestLogging(o => o.GetLevel = RequestLogging.GetLevel);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapVibeZoneHealthEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json");
}

var api = app.MapGroup("/api/v1").RequireCsrfForUnsafeMethods();
api.MapCsrfEndpoint();
modules.MapEndpoints(api);
api.MapOutboxAdminEndpoints();

app.Run();
return 0;

public partial class Program;
