using VibeZone.Infrastructure.Modules;
using VibeZone.Modules.Catalog;
using VibeZone.Modules.Identity;
using VibeZone.Modules.Media;
using VibeZone.Modules.Playlists;

namespace VibeZone.Migrations;

/// <summary>
/// The single list of modules. The database model (and therefore migrations) is built from it,
/// so every host must use this catalog.
/// </summary>
public static class AppModules
{
    public static ModuleCatalog Create() => new(
        new IdentityModule(),
        new CatalogModule(),
        new MediaModule(),
        new PlaylistsModule());
}
