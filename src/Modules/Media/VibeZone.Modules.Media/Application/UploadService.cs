using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Application;

public sealed record UploadRequest(Guid TrackId, Guid UserId, long DeclaredLength, string? FileName, string? ContentType, Stream Body);

/// <summary>
/// A new track in one request: metadata, the audio (already spooled to disk by the endpoint) and an
/// optional cover image.
/// </summary>
public sealed record NewTrackUpload(Guid UserId, NewTrackDraft Track, SpooledUpload? Audio, string? FileName, string? ContentType, byte[]? Artwork);

/// <summary>
/// Receives an original upload: owner check → size limits enforced while reading → first-bytes sniff →
/// private storage under a server-generated key → job queued and track marked Processing (one commit).
/// </summary>
public sealed partial class UploadService(
    ITrackCatalog catalog,
    IMediaRepository repository,
    IObjectStorage storage,
    ArtworkService artwork,
    IOptions<MediaOptions> options,
    TimeProvider time,
    ILogger<UploadService> logger)
{
    private static readonly DomainError TrackNotFound = DomainError.NotFound("track_not_found", "Track not found.");

    public static DomainError FileTooLarge(long maxBytes, string? field = null) =>
        DomainError.Validation("file_too_large", $"Files can be at most {maxBytes / (1024 * 1024)} MB.", field);

    private static DomainError UnsupportedFileType(string? field = null) =>
        DomainError.Validation("unsupported_file_type", "This file doesn't look like a supported audio file.", field);

    private static readonly DomainError Incomplete = DomainError.Validation("upload_incomplete", "The upload was incomplete. Try again.");

    public string TempDirectory => options.Value.ResolveTempDirectory();

    /// <summary>Uploads audio for an existing Draft/Failed/Cancelled track (raw body).</summary>
    public async Task<Result<UploadStatusDto>> StartAsync(UploadRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var track = await catalog.FindOwnedAsync(request.TrackId, request.UserId, cancellationToken);
        if (track is null)
        {
            return TrackNotFound;
        }

        if (track.Status is not (TrackLifecycle.Draft or TrackLifecycle.Failed or TrackLifecycle.Cancelled))
        {
            return DomainError.Conflict("track_not_uploadable", "This track already has audio or is being processed.");
        }

        if (await repository.FindActiveJobAsync(request.TrackId, cancellationToken) is not null)
        {
            return DomainError.Conflict("upload_in_progress", "An upload for this track is already being processed.");
        }

        if (request.DeclaredLength <= 0)
        {
            return DomainError.Validation("upload_length_required", "The upload size is missing.");
        }

        if (request.DeclaredLength > settings.MaxUploadBytes)
        {
            return FileTooLarge(settings.MaxUploadBytes);
        }

        SpooledUpload spooled;
        try
        {
            spooled = await SpooledUpload.CreateAsync(request.Body, settings.ResolveTempDirectory(), settings.MaxUploadBytes, cancellationToken);
        }
        catch (IOException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The body ended early (client/proxy dropped the connection; Kestrel reports this as an
            // IOException). A client-side problem, not a server error.
            LogIncomplete(logger, ex, request.TrackId, request.DeclaredLength);
            return Incomplete;
        }

        using (spooled)
        {
            if (spooled.TooLarge)
            {
                return FileTooLarge(settings.MaxUploadBytes);
            }

            if (spooled.Length != request.DeclaredLength)
            {
                // Protects against silently truncated bodies (e.g. by an intermediate proxy).
                return Incomplete;
            }

            if (!AudioSignature.LooksLikeAudio(spooled.Head))
            {
                return UnsupportedFileType();
            }

            var job = await QueueAsync(request.TrackId, request.UserId, spooled, request.FileName, request.ContentType, cancellationToken);
            await catalog.StageProcessingAsync(request.TrackId, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            LogQueued(logger, job.Id, request.TrackId, spooled.Length);
            return ToDto(request.TrackId, job);
        }
    }

    /// <summary>
    /// Creates a track and queues its audio (plus the cover, if any) in one commit: either everything exists
    /// afterwards or nothing does. Validation order: metadata → audio → cover.
    /// </summary>
    public async Task<Result<UploadStatusDto>> CreateTrackAsync(NewTrackUpload request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var staged = await catalog.StageNewTrackAsync(request.UserId, request.Track, cancellationToken);
        if (!staged.IsSuccess)
        {
            return staged.Error!;
        }

        if (request.Audio is not { Length: > 0 } audio)
        {
            return DomainError.Validation("audio_required", "Choose an audio file.", "audio");
        }

        if (audio.TooLarge)
        {
            return FileTooLarge(settings.MaxUploadBytes, "audio");
        }

        if (!AudioSignature.LooksLikeAudio(audio.Head))
        {
            return UnsupportedFileType("audio");
        }

        var trackId = staged.Value;
        var written = new List<string>();
        // The track row must exist before its media rows (cross-module FKs EF doesn't know about), so this
        // takes two saves; the transaction keeps it all or nothing.
        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        try
        {
            await repository.SaveChangesAsync(cancellationToken);

            if (request.Artwork is { } image)
            {
                var cover = await artwork.StageAsync(trackId, image, cancellationToken);
                if (!cover.IsSuccess)
                {
                    return cover.Error!; // disposing the transaction rolls the track back
                }

                written.AddRange(cover.Value.NewKeys);
            }

            var job = await QueueAsync(trackId, request.UserId, audio, request.FileName, request.ContentType, cancellationToken);
            written.Add(job.ObjectKey);
            await catalog.StageProcessingAsync(trackId, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            await repository.CommitAsync(transaction, cancellationToken);
            LogQueued(logger, job.Id, trackId, audio.Length);
            return ToDto(trackId, job);
        }
        catch
        {
            await artwork.DeleteQuietlyAsync(written);
            throw;
        }
    }

    public async Task<Result> CancelAsync(Guid userId, Guid trackId, CancellationToken cancellationToken)
    {
        if (await catalog.FindOwnedAsync(trackId, userId, cancellationToken) is not { } track)
        {
            return Result.Failure(TrackNotFound);
        }

        var job = await repository.FindActiveJobAsync(trackId, cancellationToken);
        if (job is null)
        {
            return Result.Failure(DomainError.Conflict("no_active_upload", "There is no upload in progress."));
        }

        job.Cancel(time.GetUtcNow());
        if (track.Status == TrackLifecycle.Processing)
        {
            await catalog.StageCancelledAsync(trackId, cancellationToken);
        }

        // The worker notices on its next heartbeat and abandons the job.
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<UploadStatusDto>> GetStatusAsync(Guid userId, Guid trackId, CancellationToken cancellationToken)
    {
        if (await catalog.FindOwnedAsync(trackId, userId, cancellationToken) is null)
        {
            return TrackNotFound;
        }

        var job = await repository.FindLatestJobAsync(trackId, cancellationToken);
        return job is null ? new UploadStatusDto(trackId, null, null, 0, null, null, null) : ToDto(trackId, job);
    }

    /// <summary>Stores the verified original and stages its job (the caller commits).</summary>
    private async Task<UploadJob> QueueAsync(Guid trackId, Guid userId, SpooledUpload spooled, string? fileName, string? contentType, CancellationToken cancellationToken)
    {
        var key = MediaAsset.NewKey(trackId, MediaAssetKind.Original, string.Empty);
        await using (var file = File.OpenRead(spooled.Path))
        {
            await storage.PutAsync(key, file, spooled.Length, "application/octet-stream", cancellationToken);
        }

        var job = UploadJob.CreateUploaded(trackId, userId, key, spooled.Length, spooled.Sha256, fileName, contentType, options.Value.MaxAttempts, time.GetUtcNow());
        repository.Add(job);
        return job;
    }

    private static UploadStatusDto ToDto(Guid trackId, UploadJob job) =>
        new(trackId, job.Status, job.LastError, job.AttemptCount, job.SizeBytes, job.OriginalFileName, job.UpdatedAt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload for track {TrackId} ended before {DeclaredBytes} bytes were received")]
    private static partial void LogIncomplete(ILogger logger, Exception ex, Guid trackId, long declaredBytes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Upload job {JobId} queued for track {TrackId} ({Bytes} bytes)")]
    private static partial void LogQueued(ILogger logger, Guid jobId, Guid trackId, long bytes);
}
