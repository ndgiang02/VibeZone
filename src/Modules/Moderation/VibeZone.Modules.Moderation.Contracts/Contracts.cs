using VibeZone.SharedKernel;

namespace VibeZone.Modules.Moderation.Contracts;

/// <summary>
/// Append-only record of staff actions. <see cref="Stage"/> adds the entry to the shared unit of work so it
/// commits with the action itself. Metadata must never hold secrets, emails or tokens.
/// </summary>
public interface IAuditLog
{
    void Stage(Guid actorUserId, string action, string subjectType, string? subjectId, IReadOnlyDictionary<string, string>? metadata = null);
}

public static class AuditActions
{
    public const string RoleGranted = "role_granted";
    public const string RoleRevoked = "role_revoked";
    public const string ReportDismissed = "report_dismissed";
    public const string ContentRemoved = "content_removed";
    public const string ContentRestored = "content_restored";
}

/// <param name="Outcome"><c>actioned</c> (content removed) or <c>rejected</c>.</param>
public sealed record ReportResolved(Guid ReportId, Guid ReporterUserId, string SubjectType, Guid SubjectId, string SubjectTitle, string Outcome) : IIntegrationEvent
{
    public static string EventType => "moderation.report_resolved";

    public static int EventVersion => 1;
}

/// <param name="SubjectType"><c>track</c>, <c>comment</c> or <c>playlist</c>.</param>
/// <param name="Reason">Report reason code (copyright, abuse, spam, other).</param>
public sealed record ContentRemoved(string SubjectType, Guid SubjectId, Guid OwnerUserId, string SubjectTitle, string Reason) : IIntegrationEvent
{
    public static string EventType => "moderation.content_removed";

    public static int EventVersion => 1;
}
