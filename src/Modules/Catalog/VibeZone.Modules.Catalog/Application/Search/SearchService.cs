using System.Text.RegularExpressions;
using VibeZone.Modules.Catalog.Application.Tracks;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Application.Search;

public sealed record SearchSection<T>(IReadOnlyList<T> Items, int Total);

/// <summary>First results of each kind; page through one kind with <c>/search/{kind}</c>.</summary>
public sealed record SearchResultsDto(string Query, SearchSection<TrackDto> Tracks, SearchSection<ArtistDto> Artists, SearchSection<AlbumDto> Albums);

/// <summary>
/// Full-text + trigram search over listed content only (Ready public tracks, all artists, public albums).
/// Accent-insensitive ("son tung" finds "Sơn Tùng"), prefix matching while typing, tolerant to small typos.
/// </summary>
public interface ISearchQueries
{
    Task<Paged<Guid>> TrackIdsAsync(SearchText text, PageRequest page, CancellationToken cancellationToken);

    Task<Paged<Guid>> ArtistIdsAsync(SearchText text, PageRequest page, CancellationToken cancellationToken);

    Task<Paged<Guid>> AlbumIdsAsync(SearchText text, PageRequest page, CancellationToken cancellationToken);
}

/// <summary>A normalised query: the raw text (for similarity) and a prefix tsquery like <c>son:* &amp; tung:*</c>.</summary>
public sealed partial record SearchText(string Raw, string PrefixQuery)
{
    public const int MaxLength = 100;
    private const int MaxTerms = 8;

    /// <summary>Null when the query has no letters or digits (nothing to search).</summary>
    public static SearchText? Parse(string? query)
    {
        var raw = (query ?? string.Empty).Trim();
        raw = raw.Length > MaxLength ? raw[..MaxLength] : raw;
        // Only letter/digit runs reach to_tsquery, so its operators can't be injected.
        var terms = Term().Matches(raw).Select(m => m.Value.ToLowerInvariant()).Distinct().Take(MaxTerms).ToList();
        return terms.Count == 0 ? null : new SearchText(raw, string.Join(" & ", terms.Select(t => t + ":*")));
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex Term();
}

public sealed class SearchService(ISearchQueries queries, ICatalogRepository repository, AlbumService albums)
{
    private const int OverviewSize = 6;

    public async Task<SearchResultsDto> OverviewAsync(string? query, Guid? viewerUserId, CancellationToken cancellationToken)
    {
        var page = new PageRequest(1, OverviewSize);
        var tracks = await TracksAsync(query, page, viewerUserId, cancellationToken);
        var artists = await ArtistsAsync(query, page, cancellationToken);
        var found = await AlbumsAsync(query, page, viewerUserId, cancellationToken);
        return new SearchResultsDto(
            query?.Trim() ?? string.Empty,
            new SearchSection<TrackDto>(tracks.Items, tracks.Total),
            new SearchSection<ArtistDto>(artists.Items, artists.Total),
            new SearchSection<AlbumDto>(found.Items, found.Total));
    }

    public async Task<Paged<TrackDto>> TracksAsync(string? query, PageRequest page, Guid? viewerUserId, CancellationToken cancellationToken)
    {
        if (SearchText.Parse(query) is not { } text)
        {
            return Empty<TrackDto>(page);
        }

        var ids = await queries.TrackIdsAsync(text, page, cancellationToken);
        var rows = (await repository.ListTracksAsync(ids.Items, cancellationToken)).ToDictionary(r => r.Track.Id);
        return ids.Map(id => rows[id].ToDto(isOwner: viewerUserId == rows[id].Artist.UserId));
    }

    public async Task<Paged<ArtistDto>> ArtistsAsync(string? query, PageRequest page, CancellationToken cancellationToken)
    {
        if (SearchText.Parse(query) is not { } text)
        {
            return Empty<ArtistDto>(page);
        }

        var ids = await queries.ArtistIdsAsync(text, page, cancellationToken);
        var artists = (await repository.ListArtistsAsync(ids.Items, cancellationToken)).ToDictionary(a => a.Id);
        return ids.Map(id => artists[id].ToDto());
    }

    public async Task<Paged<AlbumDto>> AlbumsAsync(string? query, PageRequest page, Guid? viewerUserId, CancellationToken cancellationToken)
    {
        if (SearchText.Parse(query) is not { } text)
        {
            return Empty<AlbumDto>(page);
        }

        var ids = await queries.AlbumIdsAsync(text, page, cancellationToken);
        var rows = (await repository.ListAlbumRowsAsync(ids.Items, cancellationToken)).ToDictionary(r => r.Album.Id);
        var dtos = (await albums.ToDtosAsync(ids.Items.Select(id => rows[id]).ToList(), viewerUserId, cancellationToken)).ToDictionary(a => a.Id);
        return ids.Map(id => dtos[id]);
    }

    private static Paged<T> Empty<T>(PageRequest page) => new([], page.Page, page.PageSize, 0);
}
