namespace VibeZone.Infrastructure.Web;

/// <summary>Named rate-limit policies registered by the API host and referenced by module endpoints.</summary>
public static class RateLimitPolicies
{
    /// <summary>Credential and email-triggering endpoints (login, register, reset…): strict per-IP limit.</summary>
    public const string Auth = "auth";

    /// <summary>Audio uploads: per-IP hourly limit (each upload can be hundreds of MB and starts a transcode).</summary>
    public const string Uploads = "uploads";
}
