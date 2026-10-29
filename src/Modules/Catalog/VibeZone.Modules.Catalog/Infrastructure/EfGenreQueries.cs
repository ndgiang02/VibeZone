using Microsoft.EntityFrameworkCore;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Catalog.Application.Genres;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Catalog.Domain;

namespace VibeZone.Modules.Catalog.Infrastructure;

internal sealed class EfGenreQueries(AppDbContext db) : IGenreQueries
{
    private const int CoversPerGenre = 3;

    public async Task<IReadOnlyList<GenreSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var genres = await db.Set<Genre>()
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new
            {
                g.Slug,
                g.Name,
                Covers = db.Set<Track>()
                    .Where(t => t.GenreId == g.Id && t.ArtworkId != null && t.Status == TrackStatus.Ready
                        && t.Visibility == TrackVisibility.Public && t.DeletedAt == null && t.TakenDownAt == null)
                    .OrderByDescending(t => t.PublishedAt).ThenByDescending(t => t.Id)
                    .Select(t => new { t.Id, ArtworkId = t.ArtworkId!.Value })
                    .Take(CoversPerGenre)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return genres
            .Select(g => new GenreSummaryDto(g.Slug, g.Name,
                g.Covers.Select(c => TrackArtworkPaths.For(c.Id, c.ArtworkId, TrackArtworkPaths.Small)).ToList()))
            .ToList();
    }
}
