using Microsoft.EntityFrameworkCore;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Catalog.Application.Search;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Infrastructure;

/// <summary>
/// Search SQL (migration <c>AddCatalogSearch</c>): generated <c>search_vector</c> columns over
/// <c>public.vz_fold(text)</c> (lower + unaccent, immutable), GIN indexes for full text and trigram.
/// A row matches on the prefix tsquery or on word similarity (typos); ranked by both.
/// Every value is a parameter; the tsquery text only contains letters, digits, <c>&amp;</c> and <c>:*</c>.
/// </summary>
internal sealed class EfSearchQueries(AppDbContext db) : ISearchQueries
{
    public async Task<Paged<Guid>> TrackIdsAsync(SearchText text, PageRequest page, CancellationToken cancellationToken)
    {
        var (raw, tsq) = (text.Raw, text.PrefixQuery);
        var total = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM catalog.tracks t JOIN catalog.artists a ON a.id = t.artist_id
            WHERE t.status = 'ready' AND t.visibility = 'public' AND t.deleted_at IS NULL AND t.taken_down_at IS NULL
              AND (t.search_vector @@ to_tsquery('simple', public.vz_fold({tsq}))
                OR a.search_vector @@ to_tsquery('simple', public.vz_fold({tsq}))
                OR public.vz_fold({raw}) <% public.vz_fold(t.title))
            """).SingleAsync(cancellationToken);
        if (total <= page.Skip)
        {
            return new Paged<Guid>([], page.Page, page.PageSize, total);
        }

        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT t.id AS "Value"
            FROM catalog.tracks t JOIN catalog.artists a ON a.id = t.artist_id
            WHERE t.status = 'ready' AND t.visibility = 'public' AND t.deleted_at IS NULL AND t.taken_down_at IS NULL
              AND (t.search_vector @@ to_tsquery('simple', public.vz_fold({tsq}))
                OR a.search_vector @@ to_tsquery('simple', public.vz_fold({tsq}))
                OR public.vz_fold({raw}) <% public.vz_fold(t.title))
            ORDER BY 2 * ts_rank(t.search_vector, to_tsquery('simple', public.vz_fold({tsq})))
                   + ts_rank(a.search_vector, to_tsquery('simple', public.vz_fold({tsq})))
                   + word_similarity(public.vz_fold({raw}), public.vz_fold(t.title)) DESC,
                     t.published_at DESC, t.id
            LIMIT {page.PageSize} OFFSET {page.Skip}
            """).ToListAsync(cancellationToken);
        return new Paged<Guid>(ids, page.Page, page.PageSize, total);
    }

    public async Task<Paged<Guid>> ArtistIdsAsync(SearchText text, PageRequest page, CancellationToken cancellationToken)
    {
        var (raw, tsq) = (text.Raw, text.PrefixQuery);
        var total = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM catalog.artists a
            WHERE a.search_vector @@ to_tsquery('simple', public.vz_fold({tsq}))
               OR public.vz_fold({raw}) <% public.vz_fold(a.name)
            """).SingleAsync(cancellationToken);
        if (total <= page.Skip)
        {
            return new Paged<Guid>([], page.Page, page.PageSize, total);
        }

        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT a.id AS "Value" FROM catalog.artists a
            WHERE a.search_vector @@ to_tsquery('simple', public.vz_fold({tsq}))
               OR public.vz_fold({raw}) <% public.vz_fold(a.name)
            ORDER BY ts_rank(a.search_vector, to_tsquery('simple', public.vz_fold({tsq})))
                   + word_similarity(public.vz_fold({raw}), public.vz_fold(a.name)) DESC, a.name, a.id
            LIMIT {page.PageSize} OFFSET {page.Skip}
            """).ToListAsync(cancellationToken);
        return new Paged<Guid>(ids, page.Page, page.PageSize, total);
    }

    public async Task<Paged<Guid>> AlbumIdsAsync(SearchText text, PageRequest page, CancellationToken cancellationToken)
    {
        var (raw, tsq) = (text.Raw, text.PrefixQuery);
        var total = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM catalog.albums al JOIN catalog.artists a ON a.id = al.artist_id
            WHERE al.visibility = 'public' AND al.deleted_at IS NULL
              AND (al.search_vector @@ to_tsquery('simple', public.vz_fold({tsq}))
                OR a.search_vector @@ to_tsquery('simple', public.vz_fold({tsq}))
                OR public.vz_fold({raw}) <% public.vz_fold(al.title))
            """).SingleAsync(cancellationToken);
        if (total <= page.Skip)
        {
            return new Paged<Guid>([], page.Page, page.PageSize, total);
        }

        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT al.id AS "Value"
            FROM catalog.albums al JOIN catalog.artists a ON a.id = al.artist_id
            WHERE al.visibility = 'public' AND al.deleted_at IS NULL
              AND (al.search_vector @@ to_tsquery('simple', public.vz_fold({tsq}))
                OR a.search_vector @@ to_tsquery('simple', public.vz_fold({tsq}))
                OR public.vz_fold({raw}) <% public.vz_fold(al.title))
            ORDER BY 2 * ts_rank(al.search_vector, to_tsquery('simple', public.vz_fold({tsq})))
                   + ts_rank(a.search_vector, to_tsquery('simple', public.vz_fold({tsq})))
                   + word_similarity(public.vz_fold({raw}), public.vz_fold(al.title)) DESC,
                     al.release_date DESC NULLS LAST, al.id
            LIMIT {page.PageSize} OFFSET {page.Skip}
            """).ToListAsync(cancellationToken);
        return new Paged<Guid>(ids, page.Page, page.PageSize, total);
    }
}
