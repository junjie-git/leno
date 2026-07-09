namespace Leno.SharedKernel.ValueObjects;

/// <summary>
/// 分页请求值对象。Page 从 1 开始，PageSize 默认 20、最大 100。
/// </summary>
public sealed record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; private set; } = 1;

    public int PageSize { get; private set; } = DefaultPageSize;

    public PageRequest() { }

    public PageRequest(int page, int pageSize)
    {
        Page = page < 1 ? 1 : page;
        PageSize = pageSize switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => pageSize
        };
    }

    /// <summary>跳过的记录数。</summary>
    public int Skip => (Page - 1) * PageSize;
}
