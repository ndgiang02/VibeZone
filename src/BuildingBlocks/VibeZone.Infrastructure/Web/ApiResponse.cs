using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using VibeZone.SharedKernel;

namespace VibeZone.Infrastructure.Web;

/// <summary>
/// Envelope for every successful JSON body (ADR-0008): <c>{ "data": … }</c>. Errors are RFC 9457 Problem
/// Details instead, so clients branch on the HTTP status, never on the body shape.
/// </summary>
public sealed record ApiResponse<T>(T Data)
    where T : notnull;

/// <summary>Envelope for collections: <c>{ "data": [ … ], "meta": { page, pageSize, total, hasMore } }</c>.</summary>
public sealed record ApiListResponse<T>(IReadOnlyList<T> Data, ListMeta Meta);

public sealed record ListMeta(int Page, int PageSize, int Total, bool HasMore)
{
    public static ListMeta For(int page, int pageSize, int total) => new(page, pageSize, total, (long)page * pageSize < total);
}

/// <summary>Typed results that wrap the payload in the API envelope.</summary>
public static class ApiResults
{
    public static Ok<ApiResponse<T>> Ok<T>(T data)
        where T : notnull => TypedResults.Ok(new ApiResponse<T>(data));

    public static Created<ApiResponse<T>> Created<T>(string uri, T data)
        where T : notnull => TypedResults.Created(uri, new ApiResponse<T>(data));

    public static Accepted<ApiResponse<T>> Accepted<T>(string uri, T data)
        where T : notnull => TypedResults.Accepted(uri, new ApiResponse<T>(data));

    /// <summary>A complete (unpaged) collection, reported as a single page.</summary>
    public static Ok<ApiListResponse<T>> List<T>(IReadOnlyList<T> items) =>
        TypedResults.Ok(new ApiListResponse<T>(items, ListMeta.For(1, items.Count, items.Count)));

    public static Ok<ApiListResponse<T>> Page<T>(Paged<T> page) =>
        TypedResults.Ok(new ApiListResponse<T>(page.Items, ListMeta.For(page.Page, page.PageSize, page.Total)));
}
