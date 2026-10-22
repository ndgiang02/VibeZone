using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Moderation.Application;
using VibeZone.Modules.Moderation.Contracts;
using VibeZone.Modules.Moderation.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Moderation.Infrastructure;

// reporter/handler/actor ids reference identity.users; those FKs are added in SQL by the migration because the
// Identity model belongs to another module (ADR-0001). Subject ids are polymorphic (no FK).

internal sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> b)
    {
        b.ToTable("content_reports", ModerationModule.Schema, t =>
        {
            t.HasCheckConstraint("ck_content_reports_subject_type", $"subject_type IN ({EnumCheck.Values<ReportSubjectType>()})");
            t.HasCheckConstraint("ck_content_reports_reason", $"reason IN ({EnumCheck.Values<ReportReason>()})");
            t.HasCheckConstraint("ck_content_reports_status", $"status IN ({EnumCheck.Values<ReportStatus>()})");
        });
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.SubjectType).HasConversion(new LowerCaseEnumConverter<ReportSubjectType>()).HasMaxLength(16);
        b.Property(r => r.Reason).HasConversion(new LowerCaseEnumConverter<ReportReason>()).HasMaxLength(16);
        b.Property(r => r.Status).HasConversion(new LowerCaseEnumConverter<ReportStatus>()).HasMaxLength(16);
        b.Property(r => r.Details).HasMaxLength(ContentReport.MaxDetailsLength);
        b.Property(r => r.ResolutionNote).HasMaxLength(ContentReport.MaxNoteLength);
        b.Ignore(r => r.IsOpen);
        b.Property<uint>("xmin").IsRowVersion();
        b.HasIndex(r => new { r.ReporterUserId, r.SubjectType, r.SubjectId }).IsUnique()
            .HasDatabaseName("ux_content_reports_open_per_reporter").HasFilter("status = 'open'");
        b.HasIndex(r => new { r.Status, r.CreatedAt });
        b.HasIndex(r => new { r.SubjectType, r.SubjectId });
    }
}

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("audit_logs", ModerationModule.Schema);
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Action).HasMaxLength(AuditLogEntry.MaxActionLength).IsRequired();
        b.Property(e => e.SubjectType).HasMaxLength(32).IsRequired();
        b.Property(e => e.SubjectId).HasMaxLength(64);
        b.Property(e => e.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
        b.HasIndex(e => new { e.SubjectType, e.SubjectId });
        b.HasIndex(e => new { e.ActorUserId, e.OccurredAt }).IsDescending(false, true);
        b.HasIndex(e => e.OccurredAt).IsDescending();
    }
}

internal sealed class EfModerationRepository(AppDbContext db) : IModerationRepository
{
    public Task<bool> HasOpenReportAsync(Guid reporterUserId, ReportSubjectType subjectType, Guid subjectId, CancellationToken cancellationToken) =>
        db.Set<ContentReport>().AnyAsync(
            r => r.ReporterUserId == reporterUserId && r.SubjectType == subjectType && r.SubjectId == subjectId && r.Status == ReportStatus.Open,
            cancellationToken);

    public Task<ContentReport?> FindAsync(Guid reportId, CancellationToken cancellationToken) =>
        db.Set<ContentReport>().SingleOrDefaultAsync(r => r.Id == reportId, cancellationToken);

    public async Task<IReadOnlyList<ContentReport>> ListOpenForSubjectAsync(ReportSubjectType subjectType, Guid subjectId, CancellationToken cancellationToken) =>
        await db.Set<ContentReport>()
            .Where(r => r.SubjectType == subjectType && r.SubjectId == subjectId && r.Status == ReportStatus.Open)
            .ToListAsync(cancellationToken);

    public async Task<Paged<ContentReport>> PageAsync(ReportStatus? status, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Set<ContentReport>().AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(r => r.Status == s);
        }

        var ordered = status == ReportStatus.Open
            ? query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            : query.OrderByDescending(r => r.HandledAt ?? r.CreatedAt).ThenByDescending(r => r.Id);
        var total = await query.CountAsync(cancellationToken);
        IReadOnlyList<ContentReport> items = total <= page.Skip ? [] : await ordered.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new Paged<ContentReport>(items, page.Page, page.PageSize, total);
    }

    public async Task<Paged<AuditLogEntry>> PageAuditAsync(PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Set<AuditLogEntry>().AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        IReadOnlyList<AuditLogEntry> items = total <= page.Skip
            ? []
            : await query.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new Paged<AuditLogEntry>(items, page.Page, page.PageSize, total);
    }

    public void Add(ContentReport report) => db.Set<ContentReport>().Add(report);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>Stages audit entries in the shared unit of work (committed with the audited change).</summary>
internal sealed class EfAuditLog(AppDbContext db, TimeProvider time) : IAuditLog
{
    public void Stage(Guid actorUserId, string action, string subjectType, string? subjectId, IReadOnlyDictionary<string, string>? metadata = null) =>
        db.Set<AuditLogEntry>().Add(AuditLogEntry.Create(
            actorUserId, action, subjectType, subjectId, metadata is { Count: > 0 } ? JsonSerializer.Serialize(metadata) : null, time.GetUtcNow()));
}
