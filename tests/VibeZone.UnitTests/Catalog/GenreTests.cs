using VibeZone.Modules.Catalog.Domain;
using VibeZone.Modules.Catalog.Infrastructure;

namespace VibeZone.UnitTests.Catalog;

public sealed class GenreTests
{
    [Fact]
    public void Creates_genre_with_valid_values()
    {
        var genre = new Genre(1, "drum-and-bass", "  Drum & Bass ");

        Assert.Equal(1, genre.Id);
        Assert.Equal("drum-and-bass", genre.Slug);
        Assert.Equal("Drum & Bass", genre.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Drum")]
    [InlineData("drum and bass")]
    [InlineData("-drum")]
    [InlineData("drum--bass")]
    [InlineData("drum_bass")]
    public void Rejects_invalid_slug(string slug) =>
        Assert.Throws<ArgumentException>(() => new Genre(1, slug, "Name"));

    [Fact]
    public void Rejects_overlong_slug() =>
        Assert.Throws<ArgumentException>(() => new Genre(1, new string('a', Genre.MaxSlugLength + 1), "Name"));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_id(int id) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Genre(id, "pop", "Pop"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank_name(string name) =>
        Assert.ThrowsAny<ArgumentException>(() => new Genre(1, "pop", name));

    [Fact]
    public void Reference_data_has_unique_ids_and_slugs()
    {
        var all = GenreReferenceData.All;

        Assert.NotEmpty(all);
        Assert.Equal(all.Length, all.Select(g => g.Id).Distinct().Count());
        Assert.Equal(all.Length, all.Select(g => g.Slug).Distinct().Count());
    }
}
