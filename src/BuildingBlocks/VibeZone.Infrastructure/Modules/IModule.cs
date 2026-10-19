using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace VibeZone.Infrastructure.Modules;

/// <summary>
/// Entry point of a business module. A host composes the modules it needs from a <see cref="ModuleCatalog"/>.
/// </summary>
public interface IModule
{
    /// <summary>Lower-case module name; also its PostgreSQL schema.</summary>
    string Name { get; }

    /// <summary>Assembly holding the module's EF Core <c>IEntityTypeConfiguration</c>s.</summary>
    Assembly Assembly => GetType().Assembly;

    void AddServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps endpoints under the versioned API group (<c>/api/v1</c>).</summary>
    void MapEndpoints(IEndpointRouteBuilder api);
}
