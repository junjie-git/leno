using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Events;

/// <summary>
/// 店铺暂停集成事件，卖家与店铺管理域发布。
/// 消费方：商品域（置店铺商品不可售）、消息通知域（通知卖家）。
/// 同时实现 <see cref="IDomainEvent"/> 以便经发件箱模式在同一事务内持久化。
/// </summary>
public sealed class ShopSuspendedEvent : IntegrationEventBase, IDomainEvent
{
    /// <summary>店铺标识。</summary>
    public Guid ShopId { get; init; }

    /// <summary>卖家账号标识（用户域 UserId）。</summary>
    public Guid SellerId { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ShopId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ShopSuspendedEvent() : base()
    {
    }

    public ShopSuspendedEvent(Guid shopId, Guid sellerId) : base()
    {
        ShopId = shopId;
        SellerId = sellerId;
    }
}
