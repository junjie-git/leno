namespace Leno.SharedContracts.Responses;

/// <summary>
/// 分页响应契约（API 层），与领域层 <c>Leno.SharedKernel.ValueObjects.PageResult</c> 区分。
/// 字段为可读可写以适配 JSON 序列化与前端模型绑定。
/// </summary>
public class PageResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    public int Total { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(Total / (double)PageSize) : 0;

    public bool HasNext => Page < TotalPages;

    public PageResult() { }

    public PageResult(IReadOnlyList<T> items, int total, int page, int pageSize)
    {
        Items = items ?? Array.Empty<T>();
        Total = total;
        Page = page;
        PageSize = pageSize;
    }
}
