using VibeZone.Infrastructure.Modules;
using VibeZone.Modules.Analytics;
using VibeZone.Modules.Catalog;
using VibeZone.Modules.Identity;
using VibeZone.Modules.Media;
using VibeZone.Modules.Moderation;
using VibeZone.Modules.Notifications;
using VibeZone.Modules.Playlists;
using VibeZone.Modules.Social;

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
        new PlaylistsModule(),
        new SocialModule(),
        new AnalyticsModule(),
        new NotificationsModule(),
        new ModerationModule());
}
