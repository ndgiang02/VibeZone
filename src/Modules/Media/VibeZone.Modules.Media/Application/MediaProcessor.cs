using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Application;

public enum ProcessingOutcome
{
    Completed,
    Retrying,
    Failed,
    Abandoned,
}

/// <summary>
/// Processes one claimed job: download original → ffprobe gate → AAC rendition → waveform peaks → assets
/// stored → job Completed + track Ready in one commit. Results are discarded if the user cancelled.
/// </summary>
public sealed partial class MediaProcessor(
    IMediaRepository repository,
    ITrackCatalog catalog,
    IObjectStorage storage,
    IMediaToolkit toolkit,
    IOptions<MediaOptions> options,
    TimeProvider time,
    ILogger<MediaProcessor> logger)
{
    private static readonly HashSet<string> AllowedFormats = new(StringComparer.Ordinal)
    {
        "mp3", "wav", "flac", "ogg", "mov", "mp4", "m4a", "3gp", "3g2", "mj2", "aiff", "matroska", "webm",
    };

    private static readonly HashSet<string> AllowedCodecs = new(StringComparer.Ordinal)
    {
        "mp3", "aac", "flac", "vorbis", "opus", "alac",
    };

    /// <param name="cancelledByUser">Set by the caller's heartbeat when the API cancelled the job.</param>
    /// <param name="rowGate">Held by the caller's heartbeat while it extends the lease; held here while committing.</param>
    public async Task<ProcessingOutcome> ProcessAsync(Guid jobId, Func<bool> cancelledByUser, JobRowGate rowGate, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var job = await repository.FindJobAsync(jobId, cancellationToken) ?? throw new InvalidOperationException($"Job {jobId} not found.");
        if (job.Status != UploadJobStatus.Processing)
        {
            return ProcessingOutcome.Abandoned;
        }

        if (job.AttemptCount > job.MaxAttempts)
        {
            return await FailAsync(jobId, rowGate, new MediaProcessingException("processing_attempts_exhausted", true, "Too many attempts."), cancellationToken);
        }

        var workDir = Directory.CreateDirectory(Path.Combine(settings.ResolveTempDirectory(), $"job-{jobId:N}")).FullName;
        var uploaded = new List<string>();
        using var timeout = new CancellationTokenSource(settings.ProcessingTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var ct = linked.Token;
        try
        {
            var originalPath = Path.Combine(workDir, "original");
            await storage.DownloadToFileAsync(job.ObjectKey, originalPath, ct);

            var probe = await toolkit.ProbeAsync(originalPath, ct);
            Validate(probe, settings);

            var playbackPath = Path.Combine(workDir, "playback.m4a");
            await toolkit.TranscodeToAacAsync(originalPath, playbackPath, ct);
            var rendition = await toolkit.ProbeAsync(playbackPath, ct);
            var durationMs = (int)Math.Round(rendition.DurationSeconds * 1000);
            var peaks = await toolkit.ComputePeaksAsync(playbackPath, rendition.DurationSeconds, settings.WaveformBuckets, ct);

            var playbackKey = MediaAsset.NewKey(job.TrackId, MediaAssetKind.PlaybackAac, ".m4a");
            var playbackSize = new FileInfo(playbackPath).Length;
            await using (var file = File.OpenRead(playbackPath))
            {
                await storage.PutAsync(playbackKey, file, playbackSize, "audio/mp4", ct);
            }

            uploaded.Add(playbackKey);
            // camelCase, matching the API contract (WaveformResponse) that the web app reads.
            var waveformJson = JsonSerializer.SerializeToUtf8Bytes(new WaveformDocument(1, durationMs, peaks.Select(p => MathF.Round(p, 3)).ToArray()), JsonSerializerOptions.Web);
            var waveformKey = MediaAsset.NewKey(job.TrackId, MediaAssetKind.WaveformPeaks, ".json");
            using (var stream = new MemoryStream(waveformJson))
            {
                await storage.PutAsync(waveformKey, stream, waveformJson.Length, "application/json", ct);
            }

            uploaded.Add(waveformKey);

            // Commit against fresh rows: the API may have cancelled meanwhile (row versions guard the race).
            using var commit = await rowGate.EnterAsync(cancellationToken);
            var fresh = await repository.FindJobAsync(jobId, cancellationToken);
            if (fresh is null || fresh.Status != UploadJobStatus.Processing || cancelledByUser())
            {
                await DeleteQuietlyAsync(uploaded);
                return ProcessingOutcome.Abandoned;
            }

            var now = time.GetUtcNow();
            // Audio assets only: the cover is managed separately (ArtworkService) and survives re-uploads.
            var replaced = (await repository.ListAssetsAsync(job.TrackId, cancellationToken)).Where(a => !MediaAsset.IsArtwork(a.Kind)).ToList();
            foreach (var old in replaced)
            {
                repository.Remove(old);
            }

            var audio = probe.Audio!;
            repository.Add(MediaAsset.Create(job.TrackId, MediaAssetKind.Original, fresh.ObjectKey, "application/octet-stream", fresh.SizeBytes, fresh.Sha256,
                (int)Math.Round(probe.DurationSeconds * 1000), audio.Codec, audio.Bitrate, audio.SampleRate, audio.Channels, now));
            repository.Add(MediaAsset.Create(job.TrackId, MediaAssetKind.PlaybackAac, playbackKey, "audio/mp4", playbackSize, null,
                durationMs, rendition.Audio?.Codec, rendition.Audio?.Bitrate, rendition.Audio?.SampleRate, rendition.Audio?.Channels, now));
            repository.Add(MediaAsset.Create(job.TrackId, MediaAssetKind.WaveformPeaks, waveformKey, "application/json", waveformJson.Length, null,
                durationMs, null, null, null, null, now));
            fresh.Complete(now);
            await catalog.StageReadyAsync(job.TrackId, durationMs, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);

            await DeleteQuietlyAsync(replaced.Where(a => a.ObjectKey != fresh.ObjectKey).Select(a => a.ObjectKey));
            LogCompleted(logger, jobId, job.TrackId, durationMs);
            return ProcessingOutcome.Completed;
        }
        catch (OperationCanceledException) when (cancelledByUser())
        {
            await DeleteQuietlyAsync(uploaded);
            return ProcessingOutcome.Abandoned;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown: leave the job leased; it becomes claimable again when the lease expires.
            await DeleteQuietlyAsync(uploaded);
            throw;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await DeleteQuietlyAsync(uploaded);
            return await FailAsync(jobId, rowGate, new MediaProcessingException("processing_timeout", true, "Processing took too long."), cancellationToken);
        }
        catch (MediaProcessingException ex)
        {
            await DeleteQuietlyAsync(uploaded);
            return await FailAsync(jobId, rowGate, ex, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or TimeoutException || ex.GetType().Namespace?.StartsWith("Amazon", StringComparison.Ordinal) == true)
        {
            await DeleteQuietlyAsync(uploaded);
            return await FailAsync(jobId, rowGate, new MediaProcessingException("processing_error", false, "Transient processing error.", ex), cancellationToken);
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private static void Validate(ProbeResult probe, MediaOptions settings)
    {
        if (probe.Audio is null)
        {
            throw new MediaProcessingException("no_audio_stream", true, "The file has no audio stream.");
        }

        var formats = probe.FormatName.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var codec = probe.Audio.Codec;
        if (!formats.Any(AllowedFormats.Contains) || !(AllowedCodecs.Contains(codec) || codec.StartsWith("pcm_", StringComparison.Ordinal)))
        {
            throw new MediaProcessingException("unsupported_format", true, $"Unsupported format '{probe.FormatName}' / codec '{codec}'.");
        }

        if (double.IsNaN(probe.DurationSeconds) || probe.DurationSeconds < settings.MinDurationSeconds)
        {
            throw new MediaProcessingException("duration_too_short", true, "The audio is too short.");
        }

        if (probe.DurationSeconds > settings.MaxDurationSeconds)
        {
            throw new MediaProcessingException("duration_too_long", true, "The audio is too long.");
        }
    }

    private async Task<ProcessingOutcome> FailAsync(Guid jobId, JobRowGate rowGate, MediaProcessingException error, CancellationToken cancellationToken)
    {
        using var commit = await rowGate.EnterAsync(cancellationToken);
        var job = await repository.FindJobAsync(jobId, cancellationToken);
        if (job is null || job.Status != UploadJobStatus.Processing)
        {
            return ProcessingOutcome.Abandoned;
        }

        var terminal = job.Fail(error.Code, error.Permanent, time.GetUtcNow());
        if (terminal)
        {
            await catalog.StageFailedAsync(job.TrackId, error.Code, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
        LogFailed(logger, error, jobId, error.Code, error.Permanent, terminal);
        return terminal ? ProcessingOutcome.Failed : ProcessingOutcome.Retrying;
    }

    private async Task DeleteQuietlyAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LogOrphan(logger, ex, key);
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Stored waveform format consumed by the web app (wavesurfer.js peaks).</summary>
    private sealed record WaveformDocument(int Version, int DurationMs, float[] Peaks);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} completed: track {TrackId} ready ({DurationMs} ms)")]
    private static partial void LogCompleted(ILogger logger, Guid jobId, Guid trackId, int durationMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} failed with {Code} (permanent: {Permanent}, terminal: {Terminal})")]
    private static partial void LogFailed(ILogger logger, Exception ex, Guid jobId, string code, bool permanent, bool terminal);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete object {Key}; it is orphaned")]
    private static partial void LogOrphan(ILogger logger, Exception ex, string key);
}
