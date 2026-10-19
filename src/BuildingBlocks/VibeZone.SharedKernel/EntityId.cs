namespace VibeZone.SharedKernel;

public static class EntityId
{
    /// <summary>Time-ordered UUIDv7 — keeps B-tree inserts local. See docs/architecture/data-model.md.</summary>
    public static Guid New() => Guid.CreateVersion7();
}
