using System.ComponentModel.DataAnnotations;
using VibeZone.Modules.Media.Domain;

namespace VibeZone.Modules.Media.Application;

public sealed class MediaOptions
{
    public const string Section = "Media";

    /// <summary>Largest accepted upload (default 200 MB).</summary>
    [Range(1, long.MaxValue)]
    public long MaxUploadBytes { get; set; } = 200L * 1024 * 1024;

    [Range(1, int.MaxValue)]
    public int MinDurationSeconds { get; set; } = 1;

    /// <summary>Longest accepted audio (default 2 h).</summary>
    [Range(1, int.MaxValue)]
    public int MaxDurationSeconds { get; set; } = 2 * 60 * 60;

    /// <summary>Hard cap for one job's FFmpeg work (probe + transcode + waveform).</summary>
    public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(15);

    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 3;

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Parallel jobs per worker process (each runs FFmpeg).</summary>
    [Range(1, 16)]
    public int MaxConcurrentJobs { get; set; } = 2;

    [Range(100, 10_000)]
    public int WaveformBuckets { get; set; } = 1000;

    /// <summary>Scratch space for uploads and processing (defaults to the system temp directory).</summary>
    public string? TempDirectory { get; set; }

    public string FfmpegPath { get; set; } = "ffmpeg";

    public string FfprobePath { get; set; } = "ffprobe";

    /// <summary>Touched by the worker loop; the container healthcheck checks its age.</summary>
    public string? HeartbeatFile { get; set; }

    public string ResolveTempDirectory() =>
        Directory.CreateDirectory(TempDirectory ?? Path.Combine(Path.GetTempPath(), "vibezone-media")).FullName;
}

public sealed record AudioStreamInfo(string Codec, int? SampleRate, int? Channels, int? Bitrate);

public sealed record ProbeResult(string FormatName, double DurationSeconds, AudioStreamInfo? Audio);

/// <summary>FFmpeg operations (Infrastructure). Paths are server-generated temp files only.</summary>
public interface IMediaToolkit
{
    Task<ProbeResult> ProbeAsync(string inputPath, CancellationToken cancellationToken);

    /// <summary>AAC-LC 256 kbps stereo 44.1 kHz in MP4 with faststart (ADR-0005).</summary>
    Task TranscodeToAacAsync(string inputPath, string outputPath, CancellationToken cancellationToken);

    /// <summary>Peak amplitudes (0..1) of the decoded audio in <paramref name="buckets"/> equal slices.</summary>
    Task<float[]> ComputePeaksAsync(string inputPath, double durationSeconds, int buckets, CancellationToken cancellationToken);
}

/// <summary>A processing failure with a stable, user-facing code. Permanent = retrying cannot help.</summary>
public sealed class MediaProcessingException(string code, bool permanent, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public string Code { get; } = code;

    public bool Permanent { get; } = permanent;
}

public interface IMediaRepository
{
    Task<UploadJob?> FindJobAsync(Guid jobId, CancellationToken cancellationToken);

    Task<UploadJob?> FindActiveJobAsync(Guid trackId, CancellationToken cancellationToken);

    Task<UploadJob?> FindLatestJobAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>Atomically claims the next due job (or one whose lease expired). Returns its id.</summary>
    Task<Guid?> ClaimNextJobAsync(string workerId, TimeSpan lease, CancellationToken cancellationToken);

    Task ExtendLeaseAsync(Guid jobId, string workerId, TimeSpan lease, CancellationToken cancellationToken);

    /// <summary>Fresh status straight from the database (detects cancellation by the API).</summary>
    Task<UploadJobStatus?> ReadStatusAsync(Guid jobId, CancellationToken cancellationToken);

    Task<MediaAsset?> FindAssetAsync(Guid trackId, MediaAssetKind kind, CancellationToken cancellationToken);

    Task<IReadOnlyList<MediaAsset>> ListAssetsAsync(Guid trackId, CancellationToken cancellationToken);

    void Add(UploadJob job);

    void Add(MediaAsset asset);

    void Remove(MediaAsset asset);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record UploadStatusDto(Guid TrackId, UploadJobStatus? Status, string? Error, int Attempts, long? SizeBytes, string? FileName, DateTimeOffset? UpdatedAt);
