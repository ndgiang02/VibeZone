using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Contracts;

/// <summary>A track finished processing (Ready) or failed. Raised on every attempt that ends.</summary>
/// <param name="ErrorCode">Stable processing error code when <paramref name="Succeeded"/> is false.</param>
public sealed record TrackProcessed(Guid TrackId, Guid OwnerUserId, string Title, bool Succeeded, string? ErrorCode) : IIntegrationEvent
{
    public static string EventType => "catalog.track_processed";

    public static int EventVersion => 1;
}
