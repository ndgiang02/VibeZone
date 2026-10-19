using VibeZone.Infrastructure.Observability;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Infrastructure.Storage;
using VibeZone.Migrations;
using VibeZone.Modules.Media;

var builder = Host.CreateApplicationBuilder(args);

builder.AddVibeZoneLogging();
builder.AddVibeZoneTelemetry("vibezone-worker");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();

// The database model is the whole module catalog (one AppDbContext); the worker only *runs* media processing.
var modules = AppModules.Create();
builder.Services.AddSingleton(modules);
builder.Services.AddVibeZonePersistence(builder.Configuration);
builder.Services.AddVibeZoneStorage(builder.Configuration);
modules.AddServices(builder.Services, builder.Configuration);
builder.Services.AddMediaProcessingWorker();

builder.Build().Run();
