using VibeZone.Modules.Moderation.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Moderation.Application;

public sealed record UserRefDto(Guid Id, string DisplayName, string Handle, string? AvatarUrl);

/// <param name="Title">Track/playlist title or comment excerpt; null if the content no longer exists.</param>
/// <param name="TrackId">The track to open: the track itself, or the commented track.</param>
/// <param name="Removed">Already taken down / removed.</param>
public sealed record ReportSubjectDto(string? Title, UserRefDto? Owner, Guid? TrackId, bool Removed);

public sealed record ReportDto(
    Guid Id,
    ReportSubjectType SubjectType,
    Guid SubjectId,
    ReportReason Reason,
    string? Details,
    ReportStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? HandledAt,
    string? ResolutionNote,
    UserRefDto? Reporter,
    UserRefDto? HandledBy,
    ReportSubjectDto Subject);

/// <summary>What the reporter gets back (no other reporters, no moderator details).</summary>
public sealed record MyReportDto(Guid Id, ReportSubjectType SubjectType, Guid SubjectId, ReportReason Reason, ReportStatus Status, DateTimeOffset CreatedAt);

public sealed record CreateReportRequest(ReportSubjectType SubjectType, Guid SubjectId, ReportReason Reason, string? Details);

/// <param name="Action"><c>dismiss</c> (nothing wrong) or <c>remove</c> (take the content down; resolves every open report on it).</param>
public sealed record ResolveReportRequest(string? Action, string? Note);

public sealed record AuditEntryDto(
    Guid Id,
    UserRefDto? Actor,
    string Action,
    string SubjectType,
    string? SubjectId,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset OccurredAt);

/// <summary>Persistence port for the Moderation module. SaveChanges also commits changes other modules staged.</summary>
public interface IModerationRepository
{
    Task<bool> HasOpenReportAsync(Guid reporterUserId, ReportSubjectType subjectType, Guid subjectId, CancellationToken cancellationToken);

    /// <summary>Tracked for update.</summary>
    Task<ContentReport?> FindAsync(Guid reportId, CancellationToken cancellationToken);

    /// <summary>Tracked for update.</summary>
    Task<IReadOnlyList<ContentReport>> ListOpenForSubjectAsync(ReportSubjectType subjectType, Guid subjectId, CancellationToken cancellationToken);

    /// <summary>Open reports oldest first (a queue); handled ones newest first.</summary>
    Task<Paged<ContentReport>> PageAsync(ReportStatus? status, PageRequest page, CancellationToken cancellationToken);

    Task<Paged<AuditLogEntry>> PageAuditAsync(PageRequest page, CancellationToken cancellationToken);

    void Add(ContentReport report);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
