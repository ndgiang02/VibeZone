using Microsoft.EntityFrameworkCore;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Catalog.Application.Genres;
using VibeZone.Modules.Catalog.Domain;

namespace VibeZone.Modules.Catalog.Infrastructure;

internal sealed class EfGenreQueries(AppDbContext db) : IGenreQueries
{
    public async Task<IReadOnlyList<GenreDto>> ListAsync(CancellationToken cancellationToken) =>
        await db.Set<Genre>()
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new GenreDto(g.Slug, g.Name))
            .ToListAsync(cancellationToken);
}
