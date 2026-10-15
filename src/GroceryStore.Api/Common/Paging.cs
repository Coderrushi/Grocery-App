using Microsoft.EntityFrameworkCore;

namespace GroceryStore.Api.Common;

/// <summary>Query-string paging parameters. Out-of-range values are clamped rather than rejected.</summary>
public sealed record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class PagingExtensions
{
    /// <summary>Counts and fetches one page. The query must already be ordered.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> orderedQuery, PageRequest request, CancellationToken ct)
    {
        var total = await orderedQuery.CountAsync(ct);
        var items = await orderedQuery
            .Skip((request.SafePage - 1) * request.SafePageSize)
            .Take(request.SafePageSize)
            .ToListAsync(ct);
        return new PagedResult<T>(items, request.SafePage, request.SafePageSize, total);
    }
}
