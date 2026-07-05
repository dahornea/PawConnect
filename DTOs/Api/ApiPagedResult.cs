namespace PawConnect.DTOs.Api;

public sealed record ApiPagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public static ApiPagedResult<T> Create(IEnumerable<T> source, int page, int pageSize)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var items = source.ToList();
        var totalCount = items.Count;
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

        return new ApiPagedResult<T>(
            items
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList(),
            normalizedPage,
            normalizedPageSize,
            totalCount,
            totalPages);
    }
}
