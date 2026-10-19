using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Domain;

public enum UploadJobStatus
{
    /// <summary>Original stored and verified; waiting for a worker.</summary>
    Uploaded,
    Processing,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>
/// One uploaded original and its processing. Doubles as the work queue (ADR-0004): workers claim jobs
/// with a lease; an expired lease (crashed worker) makes the job claimable again.
/// </summary>
public sealed class UploadJob
{
    public const int MaxFileNameLength = 255;
    public const int MaxErrorLength = 64;

    private UploadJob() { }

    public static UploadJob CreateUploaded(
        Guid trackId, Guid userId, string objectKey, long sizeBytes, string sha256, string? originalFileName, string? declaredContentType, int maxAttempts, DateTimeOffset now) => new()
        {
            Id = EntityId.New(),
            TrackId = trackId,
            UserId = userId,
            ObjectKey = objectKey,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            OriginalFileName = SanitizeFileName(originalFileName),
            DeclaredContentType = declaredContentType is { Length: <= 127 } ? declaredContentType : null,
            Status = UploadJobStatus.Uploaded,
            MaxAttempts = maxAttempts,
            NextAttemptAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public Guid Id { get; private set; }

    public Guid TrackId { get; private set; }

    public Guid UserId { get; private set; }

    public UploadJobStatus Status { get; private set; }

    /// <summary>Display only — never used to build keys, paths or commands.</summary>
    public string? OriginalFileName { get; private set; }

    /// <summary>What the client claimed; informational, never trusted.</summary>
    public string? DeclaredContentType { get; private set; }

    public long SizeBytes { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public string ObjectKey { get; private set; } = string.Empty;

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>Stable error code of the last failure (no internal details).</summary>
    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsActive => Status is UploadJobStatus.Uploaded or UploadJobStatus.Processing;

    public void Complete(DateTimeOffset now)
    {
        Status = UploadJobStatus.Completed;
        CompletedAt = now;
        ReleaseLease(now);
    }

    /// <summary>
    /// Permanent failures (invalid media) stop immediately; transient ones retry with exponential backoff
    /// until <see cref="MaxAttempts"/>. Returns true if the job is now terminally failed.
    /// </summary>
    public bool Fail(string errorCode, bool permanent, DateTimeOffset now)
    {
        LastError = errorCode.Length <= MaxErrorLength ? errorCode : errorCode[..MaxErrorLength];
        ReleaseLease(now);
        if (permanent || AttemptCount >= MaxAttempts)
        {
            Status = UploadJobStatus.Failed;
            CompletedAt = now;
            return true;
        }

        Status = UploadJobStatus.Uploaded;
        NextAttemptAt = now + TimeSpan.FromSeconds(30 * Math.Pow(2, AttemptCount - 1));
        return false;
    }

    public void Cancel(DateTimeOffset now)
    {
        Status = UploadJobStatus.Cancelled;
        CompletedAt = now;
        ReleaseLease(now);
    }

    private void ReleaseLease(DateTimeOffset now)
    {
        LockedBy = null;
        LockedUntil = null;
        UpdatedAt = now;
    }

    private static string? SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        // Keep only the last path segment and drop control characters.
        var leaf = name.Replace('\\', '/').Split('/').Last();
        var clean = new string(leaf.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length == 0 ? null : clean.Length <= MaxFileNameLength ? clean : clean[..MaxFileNameLength];
    }
}
