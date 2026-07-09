namespace Leno.SharedKernel.ValueObjects;

/// <summary>
/// 分页结果值对象（领域层），承载查询分页数据。
/// API 响应版本见 <c>Leno.SharedContracts.Responses.PageResult</c>。
/// </summary>
public sealed record PageResult<T>
{
    public IReadOnlyList<T> Items { get; private set; }

    public int Total { get; private set; }

    public int Page { get; private set; }

    public int PageSize { get; private set; }

    public PageResult() : this(Array.Empty<T>(), 0, 1, PageRequest.DefaultPageSize) { }

    public PageResult(IReadOnlyList<T> items, int total, int page, int pageSize)
    {
        Items = items ?? Array.Empty<T>();
        Total = total < 0 ? 0 : total;
        Page = page < 1 ? 1 : page;
        PageSize = pageSize < 1 ? PageRequest.DefaultPageSize : pageSize;
    }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(Total / (double)PageSize) : 0;

    public bool HasNext => Page < TotalPages;
}
