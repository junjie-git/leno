using Leno.SharedKernel.Abstractions;

namespace Leno.UserCenter.Domain.Aggregates;

/// <summary>
/// 浏览历史聚合根骨架（Task A5 占位，Task A6 从 UserAuth.Domain 迁入完整实现）。
/// </summary>
public sealed class BrowseHistory : AggregateRoot
{
    private BrowseHistory() { }

    private BrowseHistory(Guid id) : base(id) { }
}
