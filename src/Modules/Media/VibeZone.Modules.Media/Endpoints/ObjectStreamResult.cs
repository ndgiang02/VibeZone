using System.Globalization;
using Microsoft.AspNetCore.Http;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Endpoints;

/// <summary>
/// Streams a stored asset with RFC 9110 range semantics: 200 (full), 206 (single range, Content-Range) or
/// 416 (unsatisfiable, <c>Content-Range: bytes */size</c>). Bytes flow from object storage to the client
/// without buffering the file; a client disconnect aborts the upstream read.
/// </summary>
internal sealed class ObjectStreamResult(IObjectStorage storage, MediaAsset asset) : IResult
{
    private const int CopyBufferSize = 64 * 1024;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        var size = asset.SizeBytes;
        var range = RangeRequest.Parse(httpContext.Request.Headers.Range, size);

        response.Headers.AcceptRanges = "bytes";
        response.Headers.ETag = $"\"{asset.Id:N}\"";
        // Authorization can change (track made private), so only the browser may cache, briefly.
        response.Headers.CacheControl = "private, max-age=3600";

        if (range.Outcome == RangeOutcome.Unsatisfiable)
        {
            response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
            response.Headers.ContentRange = $"bytes */{size.ToString(CultureInfo.InvariantCulture)}";
            return;
        }

        ByteRange? requested = null;
        if (range.Outcome == RangeOutcome.Partial)
        {
            requested = range.Range;
            response.StatusCode = StatusCodes.Status206PartialContent;
            response.Headers.ContentRange = string.Create(CultureInfo.InvariantCulture, $"bytes {range.Range.From}-{range.Range.To}/{size}");
            response.ContentLength = range.Range.Length;
        }
        else
        {
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentLength = size;
        }

        response.ContentType = asset.ContentType;
        var aborted = httpContext.RequestAborted;
        try
        {
            await using var source = await storage.OpenReadAsync(asset.ObjectKey, requested, aborted);
            await source.CopyToAsync(response.Body, CopyBufferSize, aborted);
        }
        catch (OperationCanceledException) when (aborted.IsCancellationRequested)
        {
            // Seeking or closing the tab cancels in-flight ranges routinely; not an error.
        }
    }
}
