using Microsoft.AspNetCore.Builder;

namespace VibeZone.Infrastructure.Web;

/// <summary>
/// Marks an endpoint that never reads cookies (credentials travel in the body or a bearer header),
/// so the API's antiforgery filter does not apply to it.
/// </summary>
public sealed class CsrfExemptMetadata
{
    public static readonly CsrfExemptMetadata Instance = new();

    private CsrfExemptMetadata() { }
}

public static class CsrfExemptionExtensions
{
    public static TBuilder DisableCsrf<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.WithMetadata(CsrfExemptMetadata.Instance);
}
