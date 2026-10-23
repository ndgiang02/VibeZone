using System.ComponentModel.DataAnnotations;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Application;

public sealed class MediaOptions
{
    public const string Section = "Media";

    /// <summary>Largest accepted upload (default 200 MB).</summary>
    [Range(1, long.MaxValue)]
    public long MaxUploadBytes { get; set; } = 200L * 1024 * 1024;

    /// <summary>Largest accepted cover image (default 10 MB).</summary>
    [Range(1, 100L * 1024 * 1024)]
    public long MaxArtworkBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>Shortest accepted cover side in pixels (smaller images look blurry when shown large).</summary>
    [Range(16, 4096)]
    public int MinArtworkSide { get; set; } = 200;

    /// <summary>Largest accepted cover (decoded pixels); bounds decoder memory (default 40 MP ≈ 160 MB RGBA).</summary>
    [Range(1, 200_000_000)]
    public long MaxArtworkPixels { get; set; } = 40_000_000;

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

    /// <summary>How long a stream URL from <c>GET /tracks/{id}/playback</c> works (default 6 h: a 2 h track plus pauses).</summary>
    public TimeSpan PlaybackTokenLifetime { get; set; } = TimeSpan.FromHours(6);

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

/// <summary>Two JPEG renditions of an uploaded image (metadata stripped, orientation applied).</summary>
public sealed record ArtworkRenditions(byte[] Small, byte[] Large);

/// <summary>Output shape of an uploaded image: aspect ratio (center crop), minimum source size and the two widths.</summary>
public sealed record ImageSpec(int AspectWidth, int AspectHeight, int MinWidth, int MinHeight, int SmallWidth, int LargeWidth)
{
    /// <summary>Track cover: square, 320 / 1200 px.</summary>
    public static ImageSpec Cover(int minSide) => new(1, 1, minSide, minSide, 320, 1200);

    /// <summary>Artist profile picture: square, 160 / 640 px.</summary>
    public static ImageSpec Avatar(int minSide) => new(1, 1, minSide, minSide, 160, 640);

    /// <summary>Artist page banner: 3:1, 800 / 1600 px wide.</summary>
    public static ImageSpec Banner(int minSide) => new(3, 1, minSide * 3, minSide, 800, 1600);
}

/// <summary>
/// Decodes an untrusted image (JPEG, PNG or WebP only) and re-encodes it. Fails with
/// <c>artwork_invalid</c>, <c>artwork_too_small</c> or <c>artwork_too_large</c>.
/// </summary>
public interface IArtworkProcessor
{
    Result<ArtworkRenditions> Render(ReadOnlySpan<byte> image, ImageSpec spec, long maxPixels);
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
    /// <summary>Reads the current row, refreshing an already-loaded instance.</summary>
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

    Task<IReadOnlyList<ArtistImage>> ListArtistImagesAsync(Guid artistId, CancellationToken cancellationToken);

    Task<ArtistImage?> FindArtistImageAsync(Guid artistId, ArtistImageKind kind, CancellationToken cancellationToken);

    void Add(ArtistImage image);

    void Remove(ArtistImage image);

    Task<IReadOnlyList<UserImage>> ListUserImagesAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserImage?> FindUserImageAsync(Guid userId, UserImageKind kind, CancellationToken cancellationToken);

    void Add(UserImage image);

    void Remove(UserImage image);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// For changes that need several saves to be atomic. Cross-module FKs (media → catalog.tracks) are not in
    /// the EF model, so EF can't order a new track before its media rows within one SaveChanges.
    /// Disposing without <see cref="CommitAsync"/> rolls back.
    /// </summary>
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken);

    Task CommitAsync(IAsyncDisposable transaction, CancellationToken cancellationToken);
}

public sealed record UploadStatusDto(Guid TrackId, UploadJobStatus? Status, string? Error, int Attempts, long? SizeBytes, string? FileName, DateTimeOffset? UpdatedAt);

/// <summary>
/// Serializes writes to a claimed job's row between the worker's lease heartbeat and the processor's commits.
/// Every UPDATE bumps the row version (xmin), so an extension landing between the processor's read and its
/// SaveChanges would turn a finished job into a concurrency failure.
/// </summary>
public sealed class JobRowGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        return new Releaser(_gate);
    }

    public void Dispose() => _gate.Dispose();

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
