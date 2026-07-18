namespace Leno.SharedContracts.Events;

/// <summary>
/// 卖家入驻申请提交集成事件，卖家与店铺管理域发布。
/// 消费方：运营审核队列、消息通知域（通知运营有新申请）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class SellerRegisteredEvent : IntegrationEventBase
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

/// <summary>
/// 店铺审核通过集成事件，卖家与店铺管理域发布。
/// 消费方：用户域（分配卖家角色）、消息通知域（通知卖家店铺已开通）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ShopApprovedEvent : IntegrationEventBase
{
    /// <summary>店铺标识。</summary>
    public Guid ShopId { get; init; }

    /// <summary>卖家账号标识（用户域 UserId）。</summary>
    public Guid SellerId { get; init; }

    /// <summary>店铺名称。</summary>
    public string ShopName { get; init; } = string.Empty;

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ShopId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ShopApprovedEvent() : base()
    {
    }

    public ShopApprovedEvent(Guid shopId, Guid sellerId, string shopName) : base()
    {
        ShopId = shopId;
        SellerId = sellerId;
        ShopName = shopName;
    }
}

/// <summary>
/// 店铺暂停集成事件，卖家与店铺管理域发布。
/// 消费方：商品域（置店铺商品不可售）、消息通知域（通知卖家）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ShopSuspendedEvent : IntegrationEventBase
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

/// <summary>
/// 店铺恢复集成事件，卖家与店铺管理域发布。
/// 消费方：商品域（恢复店铺商品可售）、消息通知域（通知卖家）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ShopResumedEvent : IntegrationEventBase
{
    /// <summary>店铺标识。</summary>
    public Guid ShopId { get; init; }

    /// <summary>卖家账号标识（用户域 UserId）。</summary>
    public Guid SellerId { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ShopId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ShopResumedEvent() : base()
    {
    }

    public ShopResumedEvent(Guid shopId, Guid sellerId) : base()
    {
        ShopId = shopId;
        SellerId = sellerId;
    }
}

/// <summary>
/// 店铺关闭集成事件，卖家与店铺管理域发布。
/// 消费方：商品域（下架全部商品）、用户域（移除卖家角色）、消息通知域（通知卖家）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class ShopClosedEvent : IntegrationEventBase
{
    /// <summary>店铺标识。</summary>
    public Guid ShopId { get; init; }

    /// <summary>卖家账号标识（用户域 UserId）。</summary>
    public Guid SellerId { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => ShopId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public ShopClosedEvent() : base()
    {
    }

    public ShopClosedEvent(Guid shopId, Guid sellerId) : base()
    {
        ShopId = shopId;
        SellerId = sellerId;
    }
}

/// <summary>
/// 资质即将到期集成事件，卖家与店铺管理域发布。
/// 消费方：消息通知域（通知卖家更新资质）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class QualificationExpiringIntegrationEvent : IntegrationEventBase
{
    /// <summary>资质标识。</summary>
    public Guid QualificationId { get; init; }

    /// <summary>所属店铺标识。</summary>
    public Guid ShopId { get; init; }

    /// <summary>卖家账号标识。</summary>
    public Guid SellerId { get; init; }

    /// <summary>资质类型。</summary>
    public string QualificationType { get; init; } = string.Empty;

    /// <summary>资质编号。</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>到期日期（UTC）。</summary>
    public DateTime ExpiryDate { get; init; }

    /// <summary>距离到期剩余天数。</summary>
    public int DaysRemaining { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public QualificationExpiringIntegrationEvent() : base()
    {
    }

    public QualificationExpiringIntegrationEvent(
        Guid qualificationId,
        Guid shopId,
        Guid sellerId,
        string qualificationType,
        string number,
        DateTime expiryDate,
        int daysRemaining) : base()
    {
        QualificationId = qualificationId;
        ShopId = shopId;
        SellerId = sellerId;
        QualificationType = qualificationType;
        Number = number;
        ExpiryDate = expiryDate;
        DaysRemaining = daysRemaining;
    }
}
