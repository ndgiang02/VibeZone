namespace VibeZone.Modules.Catalog.Application.Genres;

public sealed record GenreDto(string Slug, string Name);

/// <param name="Covers">
/// Small cover URLs of up to three of the genre's newest listed tracks that have artwork, newest first; the web
/// app stacks them as the genre tile's picture. Empty when the genre has none yet.
/// </param>
public sealed record GenreSummaryDto(string Slug, string Name, IReadOnlyList<string> Covers);

/// <summary>Read-side port; implemented in Infrastructure.</summary>
public interface IGenreQueries
{
    Task<IReadOnlyList<GenreSummaryDto>> ListAsync(CancellationToken cancellationToken);
}
