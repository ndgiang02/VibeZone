using System.ComponentModel.DataAnnotations;

namespace VibeZone.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "Database";

    /// <summary>Bound from <c>ConnectionStrings:Database</c>. Supplied via environment or user-secrets, never appsettings.</summary>
    [Required(ErrorMessage = "ConnectionStrings:Database is not configured. See README (local development).")]
    public string ConnectionString { get; set; } = string.Empty;
}
