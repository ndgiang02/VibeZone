using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VibeZone.Infrastructure.Modules;
using VibeZone.Modules.Moderation.Application;
using VibeZone.Modules.Moderation.Contracts;
using VibeZone.Modules.Moderation.Endpoints;
using VibeZone.Modules.Moderation.Infrastructure;

namespace VibeZone.Modules.Moderation;

public sealed class ModerationModule : IModule
{
    public const string Schema = "moderation";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IModerationRepository, EfModerationRepository>();
        services.AddScoped<IAuditLog, EfAuditLog>();
        services.AddScoped<ReportService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api) => ModerationEndpoints.Map(api);
}
