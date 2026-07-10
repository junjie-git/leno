namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 分页结果包装，统一承载分页查询的项集合与总数。
/// 静态工厂集中在非泛型基类上，避免在泛型类型上声明静态成员（CA1000）。
/// </summary>
public class PagedResult
{
    public int Total { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    /// <summary>总页数。</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    /// <summary>创建分页结果。</summary>
    public static PagedResult<T> Create<T>(IReadOnlyList<T> items, int total, int page, int pageSize)
        => new() { Items = items, Total = total, Page = page, PageSize = pageSize };
}

/// <summary>
/// 泛型分页结果，承载当前页数据项。
/// </summary>
/// <typeparam name="T">数据项类型。</typeparam>
public sealed class PagedResult<T> : PagedResult
{
    /// <summary>当前页数据项。</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
}
