using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Application;

public sealed record UploadRequest(Guid TrackId, Guid UserId, long DeclaredLength, string? FileName, string? ContentType, Stream Body);

/// <summary>
/// Receives an original upload: owner check → size limits enforced while reading → first-bytes sniff →
/// private storage under a server-generated key → job queued and track marked Processing (one commit).
/// </summary>
public sealed partial class UploadService(
    ITrackCatalog catalog,
    IMediaRepository repository,
    IObjectStorage storage,
    IOptions<MediaOptions> options,
    TimeProvider time,
    ILogger<UploadService> logger)
{
    private static readonly DomainError TrackNotFound = DomainError.NotFound("track_not_found", "Track not found.");

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
            return DomainError.Validation("file_too_large", $"Files can be at most {settings.MaxUploadBytes / (1024 * 1024)} MB.");
        }

        var tempPath = Path.Combine(settings.ResolveTempDirectory(), $"{EntityId.New():N}.upload");
        try
        {
            Spooled spooled;
            try
            {
                spooled = await SpoolAsync(request.Body, tempPath, settings.MaxUploadBytes, cancellationToken);
            }
            catch (IOException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // The body ended early (client/proxy dropped the connection; Kestrel reports this as an
                // IOException). A client-side problem, not a server error.
                LogIncomplete(logger, ex, request.TrackId, request.DeclaredLength);
                return DomainError.Validation("upload_incomplete", "The upload was incomplete. Try again.");
            }

            if (spooled.Error is { } spoolError)
            {
                return spoolError;
            }

            if (spooled.Length != request.DeclaredLength)
            {
                // Protects against silently truncated bodies (e.g. by an intermediate proxy).
                return DomainError.Validation("upload_incomplete", "The upload was incomplete. Try again.");
            }

            if (!AudioSignature.LooksLikeAudio(spooled.Head))
            {
                return DomainError.Validation("unsupported_file_type", "This file doesn't look like a supported audio file.");
            }

            var key = MediaAsset.NewKey(request.TrackId, MediaAssetKind.Original, string.Empty);
            await using (var file = File.OpenRead(tempPath))
            {
                await storage.PutAsync(key, file, spooled.Length, "application/octet-stream", cancellationToken);
            }

            var now = time.GetUtcNow();
            var job = UploadJob.CreateUploaded(request.TrackId, request.UserId, key, spooled.Length, spooled.Sha256, request.FileName, request.ContentType, settings.MaxAttempts, now);
            repository.Add(job);
            await catalog.StageProcessingAsync(request.TrackId, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            LogQueued(logger, job.Id, request.TrackId, spooled.Length);
            return ToDto(request.TrackId, job);
        }
        finally
        {
            TryDelete(tempPath);
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

    private static UploadStatusDto ToDto(Guid trackId, UploadJob job) =>
        new(trackId, job.Status, job.LastError, job.AttemptCount, job.SizeBytes, job.OriginalFileName, job.UpdatedAt);

    private sealed record Spooled(long Length, string Sha256, byte[] Head, DomainError? Error);

    /// <summary>Copies the body to disk while hashing; stops reading as soon as the limit is exceeded.</summary>
    private static async Task<Spooled> SpoolAsync(Stream body, string path, long maxBytes, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var head = new byte[AudioSignature.BytesNeeded];
        var headLength = 0;
        long total = 0;
        var buffer = new byte[81920];

        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true);
        int read;
        while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                return new Spooled(total, string.Empty, head, DomainError.Validation("file_too_large", "The file is too large."));
            }

            if (headLength < head.Length)
            {
                var take = Math.Min(head.Length - headLength, read);
                Array.Copy(buffer, 0, head, headLength, take);
                headLength += take;
            }

            hash.AppendData(buffer, 0, read);
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return new Spooled(total, Convert.ToHexStringLower(hash.GetHashAndReset()), head[..headLength], null);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Temp directory is cleaned by the OS/container lifecycle; nothing sensitive is lost.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Upload for track {TrackId} ended before {DeclaredBytes} bytes were received")]
    private static partial void LogIncomplete(ILogger logger, Exception ex, Guid trackId, long declaredBytes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Upload job {JobId} queued for track {TrackId} ({Bytes} bytes)")]
    private static partial void LogQueued(ILogger logger, Guid jobId, Guid trackId, long bytes);
}
