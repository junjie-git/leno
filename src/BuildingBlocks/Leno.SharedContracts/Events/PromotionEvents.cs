namespace Leno.SharedContracts.Events;

/// <summary>
/// 秒杀订单创建集成事件，秒杀下单 Redis 预扣成功后由促销域发布。
/// 消费方：通知域（下单成功通知）、订单域（异步创建秒杀订单）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class SeckillOrderCreatedIntegrationEvent : IntegrationEventBase
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>商品 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>下单用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>异步创建的订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>秒杀价（单价）。</summary>
    public decimal SeckillPrice { get; init; }

    /// <summary>下单数量。</summary>
    public int Quantity { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public SeckillOrderCreatedIntegrationEvent() : base()
    {
    }

    public SeckillOrderCreatedIntegrationEvent(
        Guid activityId,
        Guid spuId,
        Guid skuId,
        Guid userId,
        Guid orderId,
        decimal seckillPrice,
        int quantity) : base()
    {
        ActivityId = activityId;
        SpuId = spuId;
        SkuId = skuId;
        UserId = userId;
        OrderId = orderId;
        SeckillPrice = seckillPrice;
        Quantity = quantity;
    }
}

/// <summary>
/// 秒杀订单确认集成事件，订单域成功创建秒杀订单后发布。
/// 消费方：促销域（标记预占记录为已履约，补偿任务跳过）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class SeckillOrderConfirmedIntegrationEvent : IntegrationEventBase
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public SeckillOrderConfirmedIntegrationEvent() : base()
    {
    }

    public SeckillOrderConfirmedIntegrationEvent(Guid activityId, Guid orderId) : base()
    {
        ActivityId = activityId;
        OrderId = orderId;
    }
}

/// <summary>
/// 秒杀订单创建失败集成事件，订单域消费 SeckillOrderCreatedIntegrationEvent 后若创建订单失败则发布此事件。
/// 消费方：促销域（回退 Redis 库存 + 回退 DB 基线）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class SeckillOrderCreationFailedIntegrationEvent : IntegrationEventBase
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>商品 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>下单用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>订单标识（与 SeckillOrderCreatedIntegrationEvent 中的 OrderId 一致）。</summary>
    public Guid OrderId { get; init; }

    /// <summary>失败原因。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>下单数量。</summary>
    public int Quantity { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public SeckillOrderCreationFailedIntegrationEvent() : base()
    {
    }

    public SeckillOrderCreationFailedIntegrationEvent(
        Guid activityId,
        Guid skuId,
        Guid userId,
        Guid orderId,
        int quantity,
        string reason) : base()
    {
        ActivityId = activityId;
        SkuId = skuId;
        UserId = userId;
        OrderId = orderId;
        Quantity = quantity;
        Reason = reason ?? string.Empty;
    }
}

/// <summary>
/// 秒杀库存售罄集成事件，秒杀活动库存扣减至 0 时由促销域发布。
/// 消费方：通知域（售罄通知）、商品域（商品页售罄标记）。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class SeckillStockSoldOutIntegrationEvent : IntegrationEventBase
{
    /// <summary>秒杀活动标识。</summary>
    public Guid ActivityId { get; init; }

    /// <summary>商品 SKU 标识。</summary>
    public Guid SkuId { get; init; }

    /// <summary>售罄时间（UTC）。</summary>
    public DateTime SoldOutAt { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public SeckillStockSoldOutIntegrationEvent() : base()
    {
    }

    public SeckillStockSoldOutIntegrationEvent(Guid activityId, Guid skuId, DateTime soldOutAt)
        : base()
    {
        ActivityId = activityId;
        SkuId = skuId;
        SoldOutAt = soldOutAt;
    }
}
