using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Media.Application;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Endpoints;

internal static class MediaEndpoints
{
    public const string UploadLengthHeader = "X-Upload-Length";
    public const string FileNameHeader = "X-File-Name";

    public static void Map(IEndpointRouteBuilder api)
    {
        var tracks = api.MapGroup("/tracks/{trackId:guid}").WithTags("Media");

        tracks.MapPut("/upload", Upload).WithName("UploadTrackAudio").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Uploads)
            .Accepts<Stream>("application/octet-stream")
            .Produces<UploadStatusDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary($"Upload the original audio as the raw request body. Send its size in {UploadLengthHeader} and optionally its (URL-encoded) name in {FileNameHeader}.");
        tracks.MapGet("/upload", GetUploadStatus).WithName("GetUploadStatus").RequireAuthorization()
            .Produces<UploadStatusDto>().ProducesProblem(StatusCodes.Status404NotFound);
        tracks.MapDelete("/upload", CancelUpload).WithName("CancelUpload").RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        tracks.MapGet("/stream", Stream).WithName("StreamTrack").ExcludeFromDescription()
            .WithSummary("Playback rendition with HTTP range support (206/416).");
        tracks.MapGet("/waveform", Waveform).WithName("GetTrackWaveform")
            .Produces<WaveformResponse>().ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static bool IsModerator(ICurrentUser user) => user.IsInRole(Roles.Moderator) || user.IsInRole(Roles.Admin);

    private static async Task<IResult> Upload(Guid trackId, HttpRequest request, UploadService uploads, ICurrentUser user, IOptions<MediaOptions> options, CancellationToken ct)
    {
        // Allow up to the configured limit (+ slack); the service enforces the exact limit while reading.
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = options.Value.MaxUploadBytes + 1024 * 1024;
        }

        _ = long.TryParse(request.Headers[UploadLengthHeader], NumberStyles.None, CultureInfo.InvariantCulture, out var declared);
        var fileName = request.Headers[FileNameHeader].ToString() is { Length: > 0 } encoded ? Uri.UnescapeDataString(encoded) : null;
        var result = await uploads.StartAsync(new UploadRequest(trackId, user.RequireUserId(), declared, fileName, request.ContentType, request.Body), ct);
        return result.ToResult(status => TypedResults.Accepted($"/api/v1/tracks/{trackId}/upload", status));
    }

    private static async Task<IResult> GetUploadStatus(Guid trackId, UploadService uploads, ICurrentUser user, CancellationToken ct) =>
        (await uploads.GetStatusAsync(user.RequireUserId(), trackId, ct)).ToResult(s => TypedResults.Ok(s));

    private static async Task<IResult> CancelUpload(Guid trackId, UploadService uploads, ICurrentUser user, CancellationToken ct) =>
        (await uploads.CancelAsync(user.RequireUserId(), trackId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> Stream(Guid trackId, PlaybackService playback, IObjectStorage storage, ICurrentUser user, CancellationToken ct)
    {
        var asset = await playback.ResolveAsync(trackId, MediaAssetKind.PlaybackAac, user.UserId, IsModerator(user), ct);
        return asset.IsSuccess ? new ObjectStreamResult(storage, asset.Value) : asset.Error!.ToProblem();
    }

    private static async Task<IResult> Waveform(Guid trackId, PlaybackService playback, IObjectStorage storage, ICurrentUser user, HttpContext http, CancellationToken ct)
    {
        var asset = await playback.ResolveAsync(trackId, MediaAssetKind.WaveformPeaks, user.UserId, IsModerator(user), ct);
        if (!asset.IsSuccess)
        {
            return asset.Error!.ToProblem();
        }

        // Parse (case-insensitively) and re-emit so the response always matches the contract, including
        // documents stored before the worker wrote camelCase. Waveforms are small (~1000 floats).
        WaveformResponse? document;
        await using (var stored = await storage.OpenReadAsync(asset.Value.ObjectKey, null, ct))
        {
            document = await JsonSerializer.DeserializeAsync<WaveformResponse>(stored, JsonSerializerOptions.Web, ct);
        }

        if (document?.Peaks is null)
        {
            return DomainError.NotFound("media_not_ready", "Waveform isn't available for this track.").ToProblem();
        }

        http.Response.Headers.CacheControl = "private, max-age=86400";
        http.Response.Headers.ETag = $"\"{asset.Value.Id:N}\"";
        return TypedResults.Ok(document);
    }
}

/// <summary>Documented shape of the waveform JSON (stored as-is by the worker).</summary>
public sealed record WaveformResponse(int Version, int DurationMs, float[] Peaks);
