using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Catalog.Contracts;
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
        api.MapPost("/track-uploads", CreateTrackUpload).WithTags("Media").WithName("CreateTrackUpload").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Uploads)
            .Accepts<TrackUploadForm>("multipart/form-data")
            .Produces<ApiResponse<UploadStatusDto>>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Create a track and upload its audio (and optional cover) in one multipart request. All or nothing: on any error no track is created. Location points at the new track.");

        var me = api.MapGroup("/me").WithTags("Media").RequireAuthorization();
        me.MapPut("/avatar", ReplaceUserAvatar).WithName("ReplaceUserAvatar").RequireRateLimiting(RateLimitPolicies.Uploads)
            .Accepts<Stream>("image/jpeg", "image/png", "image/webp")
            .Produces<ApiResponse<ImageUrlsDto>>().ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Set or replace the signed-in user's account picture: the image as the raw body (JPEG, PNG or WebP, ≥ 200×200, ≤ 10 MB). Cropped square and re-encoded.");
        me.MapDelete("/avatar", RemoveUserAvatar).WithName("RemoveUserAvatar").Produces(StatusCodes.Status204NoContent);
        api.MapGet("/users/{userId:guid}/avatar/{size}", GetUserAvatar).WithTags("Media").WithName("GetUserAvatar")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg").ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Account picture (JPEG); size is 'small' or 'large'. Use the URLs in MeDto.avatar.");

        var myArtist = api.MapGroup("/me/artist").WithTags("Media").RequireAuthorization();
        myArtist.MapPut("/{slot:regex(^(avatar|banner)$)}", ReplaceArtistImage).WithName("ReplaceArtistImage").RequireRateLimiting(RateLimitPolicies.Uploads)
            .Accepts<Stream>("image/jpeg", "image/png", "image/webp")
            .Produces<ApiResponse<ImageUrlsDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Set or replace the signed-in artist's avatar (square, ≥ 200×200) or banner (3:1, ≥ 600×200): the image as the raw body (JPEG, PNG or WebP, ≤ 10 MB). Center-cropped and re-encoded.");
        myArtist.MapDelete("/{slot:regex(^(avatar|banner)$)}", RemoveArtistImage).WithName("RemoveArtistImage")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        api.MapGet("/artists/{artistId:guid}/{slot:regex(^(avatar|banner)$)}/{size}", GetArtistImage).WithTags("Media").WithName("GetArtistImage")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg").ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Artist avatar or banner (JPEG); size is 'small' or 'large'. Public. Use the URLs in ArtistDto.avatar / banner.");

        var tracks = api.MapGroup("/tracks/{trackId:guid}").WithTags("Media");

        tracks.MapGet("/artwork/{size}", GetArtwork).WithName("GetTrackArtwork")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg").ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Cover image (square JPEG); size is 'small' (≈320 px) or 'large' (≈1200 px). Visible to whoever can see the track. Use the URLs in TrackDto.artwork.");
        tracks.MapPut("/artwork", ReplaceArtwork).WithName("ReplaceTrackArtwork").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Uploads)
            .Accepts<Stream>("image/jpeg", "image/png", "image/webp")
            .Produces<ApiResponse<TrackArtworkDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Set or replace the cover: the image file as the raw request body (JPEG, PNG or WebP).");
        tracks.MapDelete("/artwork", RemoveArtwork).WithName("RemoveTrackArtwork").RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        tracks.MapPut("/upload", Upload).WithName("UploadTrackAudio").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Uploads)
            .Accepts<Stream>("application/octet-stream")
            .Produces<ApiResponse<UploadStatusDto>>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary($"Upload the original audio as the raw request body. Send its size in {UploadLengthHeader} and optionally its (URL-encoded) name in {FileNameHeader}.");
        tracks.MapGet("/upload", GetUploadStatus).WithName("GetUploadStatus").RequireAuthorization()
            .Produces<ApiResponse<UploadStatusDto>>().ProducesProblem(StatusCodes.Status404NotFound);
        tracks.MapDelete("/upload", CancelUpload).WithName("CancelUpload").RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        tracks.MapGet("/playback", GetPlayback).WithName("GetTrackPlayback")
            .Produces<ApiResponse<PlaybackDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Stream and waveform URLs for media players. The URLs carry a short-lived token, so the player needs no Authorization header or cookie.");
        tracks.MapGet("/stream", Stream).WithName("StreamTrack").ExcludeFromDescription()
            .WithSummary("Playback rendition with HTTP range support (206/416). Credentials, or ?token= from /playback.");
        tracks.MapGet("/waveform", Waveform).WithName("GetTrackWaveform")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<WaveformResponse>>().ProducesProblem(StatusCodes.Status404NotFound);
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
        return result.ToResult(status => ApiResults.Accepted($"/api/v1/tracks/{trackId}/upload", status));
    }

    private static async Task<IResult> CreateTrackUpload(HttpRequest request, UploadService uploads, ICurrentUser user, IOptions<MediaOptions> options, CancellationToken ct)
    {
        var settings = options.Value;
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = settings.MaxUploadBytes + settings.MaxArtworkBytes + 1024 * 1024;
        }

        var userId = user.RequireUserId();
        Result<NewTrackUpload> form;
        try
        {
            form = await TrackUploadFormReader.ReadAsync(request, userId, uploads.TempDirectory, settings, ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return UploadService.FileTooLarge(settings.MaxUploadBytes, "audio").ToProblem();
        }
        catch (IOException) when (!ct.IsCancellationRequested)
        {
            // The body ended early (connection dropped): a client-side problem, not a server error.
            return DomainError.Validation("upload_incomplete", "The upload was incomplete. Try again.").ToProblem();
        }

        if (!form.IsSuccess)
        {
            return form.Error!.ToProblem();
        }

        using var audio = form.Value.Audio;
        var result = await uploads.CreateTrackAsync(form.Value, ct);
        return result.ToResult(status => ApiResults.Accepted($"/api/v1/tracks/{status.TrackId}", status));
    }

    private static async Task<IResult> GetArtwork(Guid trackId, string size, ArtworkService artwork, IObjectStorage storage, ICurrentUser user, CancellationToken ct)
    {
        var asset = await artwork.ResolveAsync(trackId, size, user.UserId, IsModerator(user), ct);
        return asset.IsSuccess ? new ObjectStreamResult(storage, asset.Value) : asset.Error!.ToProblem();
    }

    private static async Task<IResult> ReplaceArtwork(Guid trackId, HttpRequest request, ArtworkService artwork, ICurrentUser user, IOptions<MediaOptions> options, CancellationToken ct)
    {
        var maxBytes = options.Value.MaxArtworkBytes;
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = maxBytes + 1024;
        }

        byte[]? image;
        try
        {
            image = await ArtworkService.ReadLimitedAsync(request.Body, maxBytes, ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            image = null;
        }
        catch (IOException) when (!ct.IsCancellationRequested)
        {
            return DomainError.Validation("upload_incomplete", "The upload was incomplete. Try again.").ToProblem();
        }

        if (image is null)
        {
            return ArtworkService.TooLarge(maxBytes).ToProblem();
        }

        return (await artwork.ReplaceAsync(user.RequireUserId(), trackId, image, ct)).ToResult(ApiResults.Ok);
    }

    private static async Task<IResult> ReplaceUserAvatar(HttpRequest request, UserAvatarService avatars, ICurrentUser user, IOptions<MediaOptions> options, CancellationToken ct)
    {
        var maxBytes = options.Value.MaxArtworkBytes;
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = maxBytes + 1024;
        }

        byte[]? image;
        try
        {
            image = await ArtworkService.ReadLimitedAsync(request.Body, maxBytes, ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            image = null;
        }
        catch (IOException) when (!ct.IsCancellationRequested)
        {
            return DomainError.Validation("upload_incomplete", "The upload was incomplete. Try again.").ToProblem();
        }

        return image is null
            ? ArtistImageService.TooLarge(maxBytes).ToProblem()
            : (await avatars.ReplaceAsync(user.RequireUserId(), image, ct)).ToResult(ApiResults.Ok);
    }

    private static async Task<IResult> RemoveUserAvatar(UserAvatarService avatars, ICurrentUser user, CancellationToken ct) =>
        (await avatars.RemoveAsync(user.RequireUserId(), ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> GetUserAvatar(Guid userId, string size, UserAvatarService avatars, IObjectStorage storage, CancellationToken ct)
    {
        var image = await avatars.ResolveAsync(userId, size, ct);
        return image.IsSuccess ? new ObjectStreamResult(storage, image.Value) : image.Error!.ToProblem();
    }

    private static ArtistImageSlot ParseSlot(string slot) => slot == "avatar" ? ArtistImageSlot.Avatar : ArtistImageSlot.Banner;

    private static async Task<IResult> ReplaceArtistImage(string slot, HttpRequest request, ArtistImageService images, ICurrentUser user, IOptions<MediaOptions> options, CancellationToken ct)
    {
        var maxBytes = options.Value.MaxArtworkBytes;
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = maxBytes + 1024;
        }

        byte[]? image;
        try
        {
            image = await ArtworkService.ReadLimitedAsync(request.Body, maxBytes, ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            image = null;
        }
        catch (IOException) when (!ct.IsCancellationRequested)
        {
            return DomainError.Validation("upload_incomplete", "The upload was incomplete. Try again.").ToProblem();
        }

        return image is null
            ? ArtistImageService.TooLarge(maxBytes).ToProblem()
            : (await images.ReplaceAsync(user.RequireUserId(), ParseSlot(slot), image, ct)).ToResult(ApiResults.Ok);
    }

    private static async Task<IResult> RemoveArtistImage(string slot, ArtistImageService images, ICurrentUser user, CancellationToken ct) =>
        (await images.RemoveAsync(user.RequireUserId(), ParseSlot(slot), ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> GetArtistImage(Guid artistId, string slot, string size, ArtistImageService images, IObjectStorage storage, CancellationToken ct)
    {
        var image = await images.ResolveAsync(artistId, ParseSlot(slot), size, ct);
        return image.IsSuccess ? new ObjectStreamResult(storage, image.Value) : image.Error!.ToProblem();
    }

    private static async Task<IResult> RemoveArtwork(Guid trackId, ArtworkService artwork, ICurrentUser user, CancellationToken ct) =>
        (await artwork.RemoveAsync(user.RequireUserId(), trackId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> GetUploadStatus(Guid trackId, UploadService uploads, ICurrentUser user, CancellationToken ct) =>
        (await uploads.GetStatusAsync(user.RequireUserId(), trackId, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> CancelUpload(Guid trackId, UploadService uploads, ICurrentUser user, CancellationToken ct) =>
        (await uploads.CancelAsync(user.RequireUserId(), trackId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> GetPlayback(Guid trackId, PlaybackService playback, ICurrentUser user, HttpContext http, CancellationToken ct)
    {
        var ticket = await playback.CreateTicketAsync(trackId, user.UserId, IsModerator(user), ct);
        if (!ticket.IsSuccess)
        {
            return ticket.Error!.ToProblem();
        }

        http.Response.Headers.CacheControl = "no-store";
        var (audio, token, expiresAt) = ticket.Value;
        var query = "?token=" + Uri.EscapeDataString(token);
        return ApiResults.Ok(new PlaybackDto(
            trackId,
            $"/api/v1/tracks/{trackId}/stream{query}",
            $"/api/v1/tracks/{trackId}/waveform{query}",
            audio.ContentType,
            audio.SizeBytes,
            audio.DurationMs,
            expiresAt));
    }

    private static Task<Result<MediaAsset>> Resolve(Guid trackId, MediaAssetKind kind, string? token, PlaybackService playback, ICurrentUser user, CancellationToken ct) =>
        token is null
            ? playback.ResolveAsync(trackId, kind, user.UserId, IsModerator(user), ct)
            : playback.ResolveWithTokenAsync(trackId, kind, token, ct);

    private static async Task<IResult> Stream(Guid trackId, string? token, PlaybackService playback, IObjectStorage storage, ICurrentUser user, CancellationToken ct)
    {
        var asset = await Resolve(trackId, MediaAssetKind.PlaybackAac, token, playback, user, ct);
        return asset.IsSuccess ? new ObjectStreamResult(storage, asset.Value) : asset.Error!.ToProblem();
    }

    private static async Task<IResult> Waveform(Guid trackId, string? token, PlaybackService playback, IObjectStorage storage, ICurrentUser user, HttpContext http, CancellationToken ct)
    {
        var asset = await Resolve(trackId, MediaAssetKind.WaveformPeaks, token, playback, user, ct);
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
        return ApiResults.Ok(document);
    }
}

/// <param name="StreamUrl">Relative to the API origin; AAC in MP4 with HTTP range support. Works until <paramref name="ExpiresAt"/>.</param>
/// <param name="WaveformUrl">Relative to the API origin; peaks JSON (see <see cref="WaveformResponse"/>).</param>
public sealed record PlaybackDto(Guid TrackId, string StreamUrl, string WaveformUrl, string ContentType, long SizeBytes, int? DurationMs, DateTimeOffset ExpiresAt);

/// <summary>Documented shape of the waveform JSON (stored as-is by the worker).</summary>
public sealed record WaveformResponse(int Version, int DurationMs, float[] Peaks);
