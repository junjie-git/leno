using Leno.SharedKernel.Abstractions;

namespace Leno.Cart.Domain.Events;

/// <summary>
/// 领域事件：匿名购物车合并到用户购物车。
/// 由 Cart 聚合在登录合并场景中收集，mapper 翻译为
/// <see cref="Leno.SharedContracts.Events.CartMergedEvent"/> 集成事件对外发布。
/// 消费方：数据分析域（用户行为追踪）、消息通知域（可选）。
/// </summary>
public sealed class CartMergedDomainEvent : DomainEventBase
{
    /// <summary>买家账号标识（用户域 UserId）。</summary>
    public Guid UserId { get; init; }

    /// <summary>匿名会话标识（合并前匿名购物车的 SessionId）。</summary>
    public string AnonymousId { get; init; } = string.Empty;

    /// <summary>合并的购物车项数量。</summary>
    public int MergedItemCount { get; init; }

    public CartMergedDomainEvent(Guid cartId, Guid userId, string anonymousId, int mergedItemCount)
        : base(cartId)
    {
        UserId = userId;
        AnonymousId = anonymousId ?? string.Empty;
        MergedItemCount = mergedItemCount;
    }
}
