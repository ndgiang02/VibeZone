using Microsoft.EntityFrameworkCore;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Media.Application;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Infrastructure;

internal sealed class EfJobQueries(AppDbContext db) : IJobQueries
{
    public async Task<Paged<UploadJob>> PageAsync(UploadJobStatus? status, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Set<UploadJob>().AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(j => j.Status == s);
        }

        var total = await query.CountAsync(cancellationToken);
        IReadOnlyList<UploadJob> items = total <= page.Skip
            ? []
            : await query.OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new Paged<UploadJob>(items, page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyDictionary<UploadJobStatus, int>> CountByStatusAsync(CancellationToken cancellationToken) =>
        await db.Set<UploadJob>().AsNoTracking().GroupBy(j => j.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
}
