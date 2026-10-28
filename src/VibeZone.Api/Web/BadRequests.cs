using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace VibeZone.Api.Web;

/// <summary>
/// Requests the framework can't bind (unreadable JSON, a wrongly typed field, a missing body) become 400 Problem
/// Details with the stable code <c>request_invalid</c> in every environment, instead of a 500 (Development, where
/// minimal APIs throw) or an empty 400 (elsewhere).
/// </summary>
internal static class BadRequests
{
    public const string Code = "request_invalid";
    public const string TooLargeCode = "request_too_large";

    public const string UnsupportedMediaTypeCode = "unsupported_media_type";

    /// <summary>
    /// Gives the framework's own 415 (routing rejects a body whose Content-Type the endpoint doesn't accept, before the
    /// handler runs) a stable code like every other error.
    /// </summary>
    public static void AddFrameworkCode(ProblemDetails problem)
    {
        if (problem.Status == StatusCodes.Status415UnsupportedMediaType)
        {
            problem.Extensions.TryAdd("code", UnsupportedMediaTypeCode);
        }
    }

    public static IServiceCollection AddVibeZoneBadRequestHandling(this IServiceCollection services) =>
        services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true)
            .AddExceptionHandler<BadRequestExceptionHandler>();

    private sealed class BadRequestExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is not BadHttpRequestException bad || httpContext.Response.HasStarted)
            {
                return false;
            }

            var tooLarge = bad.StatusCode == StatusCodes.Status413PayloadTooLarge;
            httpContext.Response.StatusCode = bad.StatusCode;
            // The exception message can echo parts of the request, so it is not returned.
            return await problems.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = bad.StatusCode,
                    Title = tooLarge ? "The request body is too large." : "The request is malformed.",
                    Extensions = { ["code"] = tooLarge ? TooLargeCode : Code },
                },
            });
        }
    }
}
