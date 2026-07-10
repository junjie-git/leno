using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Events;

/// <summary>
/// 卖家入驻申请提交集成事件，卖家与店铺管理域发布。
/// 消费方：运营审核队列、消息通知域（通知运营有新申请）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class SellerRegisteredEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>店铺标识（入驻申请生成的店铺主体）。</summary>
    public Guid ShopId { get; init; }

    /// <summary>卖家账号标识（用户域 UserId，与 UserId 字段一致）。</summary>
    public Guid SellerId { get; init; }

    /// <summary>卖家账号标识（用户域 UserId）。</summary>
    public Guid UserId { get; init; }

    /// <summary>申请的店铺名称。</summary>
    public string ShopName { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ShopId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public SellerRegisteredEvent() : base()
    {
    }

    public SellerRegisteredEvent(Guid shopId, Guid sellerId, Guid userId, string shopName) : base()
    {
        ShopId = shopId;
        SellerId = sellerId;
        UserId = userId;
        ShopName = shopName;
    }
}
