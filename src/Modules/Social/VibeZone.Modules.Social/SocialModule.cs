using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VibeZone.Infrastructure.Modules;
using VibeZone.Modules.Social.Application;
using VibeZone.Modules.Social.Contracts;
using VibeZone.Modules.Social.Endpoints;
using VibeZone.Modules.Social.Infrastructure;

namespace VibeZone.Modules.Social;

public sealed class SocialModule : IModule
{
    public const string Schema = "social";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISocialRepository, EfSocialRepository>();
        services.AddScoped<LikeService>();
        services.AddScoped<FollowService>();
        services.AddScoped<CommentService>();
        services.AddScoped<ILikedTracks, EfLikedTracks>();
        services.AddScoped<ICommentModeration, EfCommentModeration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api) => SocialEndpoints.Map(api);
}
