using VibeZone.Modules.Catalog.Domain;

namespace VibeZone.Modules.Catalog.Infrastructure;

/// <summary>
/// Initial genre taxonomy. Ids are stable forever: add new genres with new ids in a migration,
/// never renumber or reuse.
/// </summary>
internal static class GenreReferenceData
{
    public static readonly Genre[] All =
    [
        new(1, "electronic", "Electronic"),
        new(2, "house", "House"),
        new(3, "techno", "Techno"),
        new(4, "drum-and-bass", "Drum & Bass"),
        new(5, "dubstep", "Dubstep"),
        new(6, "ambient", "Ambient"),
        new(7, "lo-fi", "Lo-fi"),
        new(8, "hip-hop", "Hip-Hop"),
        new(9, "rnb", "R&B"),
        new(10, "pop", "Pop"),
        new(11, "v-pop", "V-Pop"),
        new(12, "k-pop", "K-Pop"),
        new(13, "rock", "Rock"),
        new(14, "indie", "Indie"),
        new(15, "metal", "Metal"),
        new(16, "jazz", "Jazz"),
        new(17, "classical", "Classical"),
        new(18, "folk", "Folk"),
        new(19, "country", "Country"),
        new(20, "latin", "Latin"),
        new(21, "reggae", "Reggae"),
        new(22, "soundtrack", "Soundtrack"),
        new(23, "podcast", "Podcast & Spoken Word"),
    ];
}
