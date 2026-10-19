using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Domain;

public enum MediaAssetKind
{
    Original,
    PlaybackAac,
    WaveformPeaks,
}

/// <summary>A stored file belonging to a track (original upload or a derived rendition).</summary>
public sealed class MediaAsset
{
    private MediaAsset() { }

    public static MediaAsset Create(
        Guid trackId, MediaAssetKind kind, string objectKey, string contentType, long sizeBytes, string? sha256,
        int? durationMs, string? codec, int? bitrate, int? sampleRate, int? channels, DateTimeOffset now) => new()
        {
            Id = EntityId.New(),
            TrackId = trackId,
            Kind = kind,
            ObjectKey = objectKey,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            DurationMs = durationMs,
            Codec = codec,
            Bitrate = bitrate,
            SampleRate = sampleRate,
            Channels = channels,
            CreatedAt = now,
        };

    public Guid Id { get; private set; }

    public Guid TrackId { get; private set; }

    public MediaAssetKind Kind { get; private set; }

    public string ObjectKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string? Sha256 { get; private set; }

    public int? DurationMs { get; private set; }

    public string? Codec { get; private set; }

    public int? Bitrate { get; private set; }

    public int? SampleRate { get; private set; }

    public int? Channels { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Server-generated key; user input never reaches object keys.</summary>
    public static string NewKey(Guid trackId, MediaAssetKind kind, string extension) =>
        $"tracks/{trackId:N}/{kind.ToString().ToLowerInvariant()}/{EntityId.New():N}{extension}";
}
