using VibeZone.SharedKernel;

namespace VibeZone.Modules.Moderation.Domain;

public enum ReportSubjectType
{
    Track,
    Comment,
    Playlist,
}

public enum ReportReason
{
    Copyright,
    Abuse,
    Spam,
    Other,
}

public enum ReportStatus
{
    Open,

    /// <summary>The content was removed.</summary>
    Actioned,

    /// <summary>Dismissed: nothing wrong found.</summary>
    Rejected,
}

/// <summary>A user's report about a piece of content. One open report per reporter and subject.</summary>
public sealed class ContentReport
{
    public const int MaxDetailsLength = 1000;
    public const int MaxNoteLength = 1000;

    private ContentReport() { }

    public static ContentReport Create(Guid reporterUserId, ReportSubjectType subjectType, Guid subjectId, ReportReason reason, string? details, DateTimeOffset now) => new()
    {
        Id = EntityId.New(),
        ReporterUserId = reporterUserId,
        SubjectType = subjectType,
        SubjectId = subjectId,
        Reason = reason,
        Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
        Status = ReportStatus.Open,
        CreatedAt = now,
    };

    public Guid Id { get; private set; }

    public Guid ReporterUserId { get; private set; }

    public ReportSubjectType SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    public ReportReason Reason { get; private set; }

    public string? Details { get; private set; }

    public ReportStatus Status { get; private set; }

    public Guid? HandledByUserId { get; private set; }

    public DateTimeOffset? HandledAt { get; private set; }

    public string? ResolutionNote { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsOpen => Status == ReportStatus.Open;

    public static DomainError? ValidateDetails(string? details) =>
        details is { Length: > MaxDetailsLength }
            ? DomainError.Validation("report_details_too_long", $"Details must be at most {MaxDetailsLength} characters.", "details")
            : null;

    public static DomainError? ValidateNote(string? note) =>
        note is { Length: > MaxNoteLength }
            ? DomainError.Validation("report_note_too_long", $"Note must be at most {MaxNoteLength} characters.", "note")
            : null;

    public void Resolve(Guid moderatorUserId, ReportStatus outcome, string? note, DateTimeOffset now)
    {
        if (!IsOpen || outcome == ReportStatus.Open)
        {
            throw new InvalidOperationException($"Report {Id} is {Status}; cannot resolve as {outcome}.");
        }

        Status = outcome;
        HandledByUserId = moderatorUserId;
        HandledAt = now;
        ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}

/// <summary>Append-only record of a staff action (never updated or deleted by the application).</summary>
public sealed class AuditLogEntry
{
    public const int MaxActionLength = 64;

    private AuditLogEntry() { }

    public static AuditLogEntry Create(Guid actorUserId, string action, string subjectType, string? subjectId, string? metadataJson, DateTimeOffset now) => new()
    {
        Id = EntityId.New(),
        ActorUserId = actorUserId,
        Action = action,
        SubjectType = subjectType,
        SubjectId = subjectId,
        MetadataJson = metadataJson,
        OccurredAt = now,
    };

    public Guid Id { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string SubjectType { get; private set; } = string.Empty;

    public string? SubjectId { get; private set; }

    /// <summary>JSON object of string values (no secrets).</summary>
    public string? MetadataJson { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
