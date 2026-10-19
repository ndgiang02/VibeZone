using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace VibeZone.Infrastructure.Modules;

public sealed class ModuleCatalog
{
    public ModuleCatalog(params IModule[] modules)
    {
        var duplicate = modules.GroupBy(m => m.Name).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Module '{duplicate.Key}' is registered more than once.");
        }

        Modules = modules;
    }

    public IReadOnlyList<IModule> Modules { get; }

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        foreach (var module in Modules)
        {
            module.AddServices(services, configuration);
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        foreach (var module in Modules)
        {
            module.MapEndpoints(api);
        }
    }
}
