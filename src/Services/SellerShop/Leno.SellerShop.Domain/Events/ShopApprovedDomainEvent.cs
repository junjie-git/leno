using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Events;

/// <summary>
/// 店铺审核通过领域事件，由 Shop 聚合在 Approve 方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.ShopApprovedEvent"/> 集成事件对外发布。
/// 消费方：用户域（分配卖家角色）、消息通知域（通知卖家店铺已开通）。
/// </summary>
public sealed class ShopApprovedDomainEvent : DomainEventBase
{
    /// <summary>店铺标识。</summary>
    public Guid ShopId { get; init; }

    /// <summary>卖家账号标识（用户域 UserId）。</summary>
    public Guid SellerId { get; init; }

    /// <summary>店铺名称。</summary>
    public string ShopName { get; init; } = string.Empty;

    public ShopApprovedDomainEvent(Guid shopId, Guid sellerId, string shopName)
        : base(shopId)
    {
        ShopId = shopId;
        SellerId = sellerId;
        ShopName = shopName;
    }
}
