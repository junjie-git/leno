using Leno.SharedKernel.Abstractions;

namespace Leno.UserCenter.Domain.Aggregates;

/// <summary>
/// 商品收藏聚合根骨架（Task A5 占位，Task A6 从 UserAuth.Domain 迁入完整实现）。
/// </summary>
public sealed class Favorite : AggregateRoot
{
    private Favorite() { }

    private Favorite(Guid id) : base(id) { }
}
