using Microsoft.AspNetCore.Http;
using VibeZone.SharedKernel;

namespace VibeZone.Infrastructure.Web;

public static class ProblemResults
{
    /// <summary>
    /// Maps an expected failure to RFC 9457 Problem Details. The stable <c>code</c> extension lets the
    /// web app show a localized message; validation errors also carry <c>errors[field] = [code]</c>.
    /// </summary>
    public static IResult ToProblem(this DomainError error)
    {
        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest,
        };

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Field is not null)
        {
            extensions["errors"] = new Dictionary<string, string[]> { [error.Field] = [error.Code] };
        }

        return TypedResults.Problem(title: error.Message, statusCode: status, extensions: extensions);
    }

    public static IResult ToResult(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.Error.ToProblem();

    public static IResult ToResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error!.ToProblem();
}
