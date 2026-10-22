using System.Text.Json;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Identity.Contracts;
using VibeZone.Modules.Moderation.Contracts;
using VibeZone.Modules.Moderation.Domain;
using VibeZone.Modules.Playlists.Contracts;
using VibeZone.Modules.Social.Contracts;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Moderation.Application;

/// <summary>
/// Reports from users and the moderators' queue. Removing content stages the takedown in the owning module
/// (through its contract), the audit entry and the notifications in one transaction.
/// </summary>
public sealed class ReportService(
    IModerationRepository repository,
    ITrackCatalog tracks,
    ICommentModeration comments,
    IPlaylistModeration playlists,
    IUserDirectory users,
    IAuditLog audit,
    IOutbox outbox,
    TimeProvider time)
{
    private static readonly DomainError ReportNotFound = DomainError.NotFound("report_not_found", "Report not found.");

    /// <summary>What moderation needs to know about a subject (null if it doesn't exist).</summary>
    private sealed record Subject(string Title, Guid OwnerUserId, Guid? TrackId, bool Removed, bool VisibleToReporter);

    public async Task<Result<MyReportDto>> CreateAsync(Guid reporterUserId, CreateReportRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.SubjectType))
        {
            return DomainError.Validation("report_subject_invalid", "Unknown subject type.", "subjectType");
        }

        if (!Enum.IsDefined(request.Reason))
        {
            return DomainError.Validation("report_reason_invalid", "Unknown reason.", "reason");
        }

        if (ContentReport.ValidateDetails(request.Details?.Trim()) is { } invalid)
        {
            return invalid;
        }

        var subject = await FindSubjectAsync(request.SubjectType, request.SubjectId, reporterUserId, cancellationToken);
        if (subject is null || subject.Removed || !subject.VisibleToReporter)
        {
            return DomainError.NotFound("report_subject_not_found", "Content not found.");
        }

        if (subject.OwnerUserId == reporterUserId)
        {
            return DomainError.Validation("report_own_content", "You can't report your own content.");
        }

        if (await repository.HasOpenReportAsync(reporterUserId, request.SubjectType, request.SubjectId, cancellationToken))
        {
            return DomainError.Conflict("report_exists", "You already reported this; a moderator will review it.");
        }

        var report = ContentReport.Create(reporterUserId, request.SubjectType, request.SubjectId, request.Reason, request.Details, time.GetUtcNow());
        repository.Add(report);
        await repository.SaveChangesAsync(cancellationToken);
        return new MyReportDto(report.Id, report.SubjectType, report.SubjectId, report.Reason, report.Status, report.CreatedAt);
    }

    public async Task<Paged<ReportDto>> ListAsync(ReportStatus? status, PageRequest page, CancellationToken cancellationToken)
    {
        var reports = await repository.PageAsync(status, page, cancellationToken);
        var subjects = new Dictionary<(ReportSubjectType, Guid), Subject?>();
        foreach (var r in reports.Items)
        {
            if (!subjects.ContainsKey((r.SubjectType, r.SubjectId)))
            {
                subjects[(r.SubjectType, r.SubjectId)] = await FindSubjectAsync(r.SubjectType, r.SubjectId, null, cancellationToken);
            }
        }

        var userIds = reports.Items.Select(r => r.ReporterUserId)
            .Concat(reports.Items.Where(r => r.HandledByUserId is not null).Select(r => r.HandledByUserId!.Value))
            .Concat(subjects.Values.Where(s => s is not null).Select(s => s!.OwnerUserId))
            .Distinct().ToList();
        var people = await users.GetAsync(userIds, cancellationToken);

        return reports.Map(r =>
        {
            var s = subjects[(r.SubjectType, r.SubjectId)];
            return new ReportDto(
                r.Id, r.SubjectType, r.SubjectId, r.Reason, r.Details, r.Status, r.CreatedAt, r.HandledAt, r.ResolutionNote,
                Ref(people, r.ReporterUserId),
                r.HandledByUserId is { } h ? Ref(people, h) : null,
                s is null ? new ReportSubjectDto(null, null, null, true) : new ReportSubjectDto(s.Title, Ref(people, s.OwnerUserId), s.TrackId, s.Removed));
        });
    }

    public async Task<Result<ReportDto>> ResolveAsync(Guid moderatorUserId, Guid reportId, ResolveReportRequest request, CancellationToken cancellationToken)
    {
        var remove = request.Action?.Trim().ToLowerInvariant() switch
        {
            "remove" => true,
            "dismiss" => false,
            _ => (bool?)null,
        };
        if (remove is null)
        {
            return DomainError.Validation("report_action_invalid", "Action must be 'dismiss' or 'remove'.", "action");
        }

        if (ContentReport.ValidateNote(request.Note?.Trim()) is { } invalid)
        {
            return invalid;
        }

        var report = await repository.FindAsync(reportId, cancellationToken);
        if (report is null)
        {
            return ReportNotFound;
        }

        if (!report.IsOpen)
        {
            return DomainError.Conflict("report_not_open", "This report was already handled.");
        }

        var subject = await FindSubjectAsync(report.SubjectType, report.SubjectId, null, cancellationToken);
        var title = subject?.Title ?? string.Empty;
        var now = time.GetUtcNow();
        var subjectType = SubjectName(report.SubjectType);
        var metadata = new Dictionary<string, string> { ["reportId"] = report.Id.ToString(), ["reason"] = ReasonName(report.Reason) };
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            metadata["note"] = request.Note.Trim();
        }

        if (remove == true)
        {
            var removed = subject is not null && !subject.Removed && await StageRemovalAsync(report.SubjectType, report.SubjectId, ReasonName(report.Reason), cancellationToken);
            // Every open report on the same content is settled by the removal.
            foreach (var open in await repository.ListOpenForSubjectAsync(report.SubjectType, report.SubjectId, cancellationToken))
            {
                open.Resolve(moderatorUserId, ReportStatus.Actioned, request.Note, now);
                outbox.Enqueue(new ReportResolved(open.Id, open.ReporterUserId, subjectType, open.SubjectId, title, "actioned"));
            }

            if (removed)
            {
                outbox.Enqueue(new ContentRemoved(subjectType, report.SubjectId, subject!.OwnerUserId, title, ReasonName(report.Reason)));
            }

            audit.Stage(moderatorUserId, AuditActions.ContentRemoved, subjectType, report.SubjectId.ToString(), metadata);
        }
        else
        {
            report.Resolve(moderatorUserId, ReportStatus.Rejected, request.Note, now);
            outbox.Enqueue(new ReportResolved(report.Id, report.ReporterUserId, subjectType, report.SubjectId, title, "rejected"));
            audit.Stage(moderatorUserId, AuditActions.ReportDismissed, subjectType, report.SubjectId.ToString(), metadata);
        }

        await repository.SaveChangesAsync(cancellationToken);
        var listed = await ListOneAsync(report, cancellationToken);
        return listed;
    }

    /// <summary>Lifts a takedown of a track or playlist (removed comments stay removed).</summary>
    public async Task<Result> RestoreAsync(Guid moderatorUserId, ReportSubjectType subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var restored = subjectType switch
        {
            ReportSubjectType.Track => await tracks.StageRestoreAsync(subjectId, cancellationToken),
            ReportSubjectType.Playlist => await playlists.StageRestoreAsync(subjectId, cancellationToken),
            _ => (bool?)null,
        };
        if (restored is null)
        {
            return Result.Failure(DomainError.Validation("restore_not_supported", "Only tracks and playlists can be restored.", "subjectType"));
        }

        if (restored == false)
        {
            return Result.Failure(DomainError.NotFound("report_subject_not_found", "Content not found."));
        }

        audit.Stage(moderatorUserId, AuditActions.ContentRestored, SubjectName(subjectType), subjectId.ToString());
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Paged<AuditEntryDto>> ListAuditAsync(PageRequest page, CancellationToken cancellationToken)
    {
        var entries = await repository.PageAuditAsync(page, cancellationToken);
        var people = await users.GetAsync(entries.Items.Select(e => e.ActorUserId).Distinct().ToList(), cancellationToken);
        return entries.Map(e => new AuditEntryDto(
            e.Id,
            Ref(people, e.ActorUserId),
            e.Action,
            e.SubjectType,
            e.SubjectId,
            e.MetadataJson is null ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(e.MetadataJson) ?? [],
            e.OccurredAt));
    }

    private async Task<ReportDto> ListOneAsync(ContentReport report, CancellationToken cancellationToken)
    {
        var subject = await FindSubjectAsync(report.SubjectType, report.SubjectId, null, cancellationToken);
        var ids = new List<Guid> { report.ReporterUserId };
        if (report.HandledByUserId is { } h)
        {
            ids.Add(h);
        }

        if (subject is not null)
        {
            ids.Add(subject.OwnerUserId);
        }

        var people = await users.GetAsync(ids.Distinct().ToList(), cancellationToken);
        return new ReportDto(
            report.Id, report.SubjectType, report.SubjectId, report.Reason, report.Details, report.Status, report.CreatedAt, report.HandledAt, report.ResolutionNote,
            Ref(people, report.ReporterUserId),
            report.HandledByUserId is { } handler ? Ref(people, handler) : null,
            subject is null ? new ReportSubjectDto(null, null, null, true) : new ReportSubjectDto(subject.Title, Ref(people, subject.OwnerUserId), subject.TrackId, subject.Removed));
    }

    private async Task<Subject?> FindSubjectAsync(ReportSubjectType type, Guid id, Guid? reporterUserId, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case ReportSubjectType.Track:
                var track = (await tracks.GetInfoAsync([id], cancellationToken)).GetValueOrDefault(id);
                return track is null
                    ? null
                    : new Subject(track.Title, track.OwnerUserId, track.TrackId, track.TakenDown,
                        reporterUserId is null || await tracks.CanViewAsync(id, reporterUserId, false, cancellationToken));
            case ReportSubjectType.Comment:
                var comment = await comments.FindAsync(id, cancellationToken);
                return comment is null
                    ? null
                    : new Subject(comment.Body.Length <= 120 ? comment.Body : comment.Body[..120] + "…", comment.AuthorUserId, comment.TrackId, comment.Removed,
                        reporterUserId is null || await tracks.CanViewAsync(comment.TrackId, reporterUserId, false, cancellationToken));
            case ReportSubjectType.Playlist:
                var playlist = await playlists.FindAsync(id, cancellationToken);
                return playlist is null
                    ? null
                    : new Subject(playlist.Title, playlist.OwnerUserId, null, playlist.TakenDown,
                        reporterUserId is null || playlist.IsPublic || playlist.OwnerUserId == reporterUserId);
            default:
                return null;
        }
    }

    private async Task<bool> StageRemovalAsync(ReportSubjectType type, Guid id, string reason, CancellationToken cancellationToken) => type switch
    {
        ReportSubjectType.Track => await tracks.StageTakedownAsync(id, reason, cancellationToken),
        ReportSubjectType.Comment => await comments.StageRemoveAsync(id, cancellationToken),
        ReportSubjectType.Playlist => await playlists.StageTakedownAsync(id, cancellationToken),
        _ => false,
    };

    private static UserRefDto? Ref(IReadOnlyDictionary<Guid, UserSummary> people, Guid id) =>
        people.TryGetValue(id, out var u) ? new UserRefDto(u.Id, u.DisplayName, u.Handle, u.AvatarUrl) : null;

    private static string SubjectName(ReportSubjectType type) => type.ToString().ToLowerInvariant();

    private static string ReasonName(ReportReason reason) => reason.ToString().ToLowerInvariant();
}
