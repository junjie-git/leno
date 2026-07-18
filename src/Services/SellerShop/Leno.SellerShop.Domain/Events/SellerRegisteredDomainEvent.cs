using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Events;

/// <summary>
/// 卖家入驻申请提交领域事件，由 Shop 聚合在 Create 工厂方法中收集。
/// mapper 翻译为 <see cref="Leno.SharedContracts.Events.SellerRegisteredEvent"/> 集成事件对外发布。
/// 消费方：运营审核队列、消息通知域（通知运营有新申请）。
/// </summary>
public sealed class SellerRegisteredDomainEvent : DomainEventBase
{
    /// <summary>店铺标识（入驻申请生成的店铺主体）。</summary>
    public Guid ShopId { get; init; }

    /// <summary>卖家账号标识（用户域 UserId，与 UserId 字段一致）。</summary>
    public Guid SellerId { get; init; }

    /// <summary>卖家账号标识（用户域 UserId）。</summary>
    public Guid UserId { get; init; }

    /// <summary>申请的店铺名称。</summary>
    public string ShopName { get; init; } = string.Empty;

    public SellerRegisteredDomainEvent(Guid shopId, Guid sellerId, Guid userId, string shopName)
        : base(shopId)
    {
        ShopId = shopId;
        SellerId = sellerId;
        UserId = userId;
        ShopName = shopName;
    }
}
