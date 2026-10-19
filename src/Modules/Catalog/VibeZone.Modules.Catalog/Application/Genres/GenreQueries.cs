namespace VibeZone.Modules.Catalog.Application.Genres;

public sealed record GenreDto(string Slug, string Name);

/// <summary>Read-side port; implemented in Infrastructure.</summary>
public interface IGenreQueries
{
    Task<IReadOnlyList<GenreDto>> ListAsync(CancellationToken cancellationToken);
}
