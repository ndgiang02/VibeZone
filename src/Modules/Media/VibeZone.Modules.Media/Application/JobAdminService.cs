using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Application;

/// <param name="OriginalFileName">Display only (sanitised client file name).</param>
public sealed record AdminJobDto(
    Guid Id,
    Guid TrackId,
    UploadJobStatus Status,
    int AttemptCount,
    int MaxAttempts,
    string? LastError,
    string? OriginalFileName,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

/// <param name="Counts">Jobs per status (every status present, zero included).</param>
public sealed record JobSummaryDto(IReadOnlyDictionary<UploadJobStatus, int> Counts);

/// <summary>Read side of the processing queue for the admin dashboard.</summary>
public interface IJobQueries
{
    /// <summary>Newest first.</summary>
    Task<Paged<UploadJob>> PageAsync(UploadJobStatus? status, PageRequest page, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<UploadJobStatus, int>> CountByStatusAsync(CancellationToken cancellationToken);
}

public sealed class JobAdminService(IJobQueries jobs)
{
    public async Task<Paged<AdminJobDto>> ListAsync(UploadJobStatus? status, PageRequest page, CancellationToken cancellationToken) =>
        (await jobs.PageAsync(status, page, cancellationToken)).Map(j => new AdminJobDto(
            j.Id, j.TrackId, j.Status, j.AttemptCount, j.MaxAttempts, j.LastError, j.OriginalFileName, j.SizeBytes, j.CreatedAt, j.UpdatedAt, j.CompletedAt));

    public async Task<JobSummaryDto> SummaryAsync(CancellationToken cancellationToken)
    {
        var counts = await jobs.CountByStatusAsync(cancellationToken);
        return new JobSummaryDto(Enum.GetValues<UploadJobStatus>().ToDictionary(s => s, s => counts.GetValueOrDefault(s)));
    }
}
