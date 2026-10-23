using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VibeZone.Infrastructure.Modules;
using VibeZone.Modules.Media.Application;
using VibeZone.Modules.Media.Endpoints;
using VibeZone.Modules.Media.Infrastructure;

namespace VibeZone.Modules.Media;

public sealed class MediaModule : IModule
{
    public const string Schema = "media";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MediaOptions>().Bind(configuration.GetSection(MediaOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<IMediaRepository, EfMediaRepository>();
        services.AddScoped<UploadService>();
        services.AddScoped<ArtworkService>();
        services.AddScoped<ArtistImageService>();
        services.AddScoped<UserAvatarService>();
        services.AddSingleton<IArtworkProcessor, SkiaArtworkProcessor>();
        services.AddScoped<PlaybackService>();
        services.AddScoped<IJobQueries, EfJobQueries>();
        services.AddScoped<JobAdminService>();
        services.AddSingleton<IPlaybackTokens, DataProtectionPlaybackTokens>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        MediaEndpoints.Map(api);
        AdminJobEndpoints.Map(api);
    }
}

public static class MediaWorkerExtensions
{
    /// <summary>Processing pipeline for the worker host only (needs FFmpeg/ffprobe on PATH or configured).</summary>
    public static IServiceCollection AddMediaProcessingWorker(this IServiceCollection services)
    {
        services.AddSingleton<IMediaToolkit, FfmpegToolkit>();
        services.AddScoped<MediaProcessor>();
        services.AddHostedService<MediaWorker>();
        return services;
    }
}
