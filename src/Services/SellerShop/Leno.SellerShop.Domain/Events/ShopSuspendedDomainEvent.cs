using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Events;

/// <summary>
/// 店铺暂停领域事件，由 Shop 聚合在 Suspend 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ShopSuspendedEvent"/> 集成事件对外发布。
/// 消费方：商品域（置店铺商品不可售）、消息通知域（通知卖家）。
/// </summary>
public sealed class ShopSuspendedDomainEvent : DomainEventBase
{
    /// <summary>店铺标识。</summary>
    public Guid ShopId { get; init; }

    /// <summary>卖家账号标识（用户域 UserId）。</summary>
    public Guid SellerId { get; init; }

    public ShopSuspendedDomainEvent(Guid shopId, Guid sellerId)
        : base(shopId)
    {
        ShopId = shopId;
        SellerId = sellerId;
    }
}
