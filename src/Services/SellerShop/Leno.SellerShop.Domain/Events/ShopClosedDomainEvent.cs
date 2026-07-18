using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Events;

/// <summary>
/// 店铺关闭领域事件，由 Shop 聚合在 Close 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ShopClosedEvent"/> 集成事件对外发布。
/// 消费方：商品域（下架全部商品）、用户域（移除卖家角色）、消息通知域（通知卖家）。
/// </summary>
public sealed class ShopClosedDomainEvent : DomainEventBase
{
    /// <summary>店铺标识。</summary>
    public Guid ShopId { get; init; }

    /// <summary>卖家账号标识（用户域 UserId）。</summary>
    public Guid SellerId { get; init; }

    public ShopClosedDomainEvent(Guid shopId, Guid sellerId)
        : base(shopId)
    {
        ShopId = shopId;
        SellerId = sellerId;
    }
}
