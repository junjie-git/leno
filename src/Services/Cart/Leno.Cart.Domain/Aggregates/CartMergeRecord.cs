namespace Leno.Cart.Domain.Aggregates;

/// <summary>
/// 匿名购物车合并记录，以 anonymousId 为主键防止重复合并导致数量翻倍。
/// 由 <c>CartAppService.MergeAnonymousCartAsync</c> 在事务内插入，依赖主键唯一约束兜底并发场景。
/// </summary>
public sealed class CartMergeRecord
{
    /// <summary>匿名会话标识（主键）。</summary>
    public string AnonymousId { get; init; } = string.Empty;

    /// <summary>合并到的用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>合并时间（UTC）。</summary>
    public DateTime MergedAt { get; init; }

    /// <summary>合并的购物车项数量。</summary>
    public int MergedCount { get; init; }
}
