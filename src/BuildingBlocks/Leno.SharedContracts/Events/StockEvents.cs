using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Events;

/// <summary>
/// 库存预占集成事件，订单域在 StockReservation 聚合预占库存时发布。
/// 消费方：对账/审计域（库存对账后台服务）、未来跨上下文消费方。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class StockReservedIntegrationEvent : IntegrationEventBase
{
    /// <summary>SKU 标识（同时为聚合根标识）。</summary>
    public Guid SkuId { get; init; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>预占数量。</summary>
    public int Quantity { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => SkuId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public StockReservedIntegrationEvent() : base()
    {
    }

    public StockReservedIntegrationEvent(Guid skuId, Guid orderId, int quantity) : base()
    {
        SkuId = skuId;
        OrderId = orderId;
        Quantity = quantity;
    }
}

/// <summary>
/// 库存确认扣减集成事件，订单域在支付成功确认预占转真实扣减时发布。
/// 消费方：对账/审计域、未来跨上下文消费方。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class StockConfirmedIntegrationEvent : IntegrationEventBase
{
    /// <summary>SKU 标识（同时为聚合根标识）。</summary>
    public Guid SkuId { get; init; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>确认扣减数量。</summary>
    public int Quantity { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => SkuId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public StockConfirmedIntegrationEvent() : base()
    {
    }

    public StockConfirmedIntegrationEvent(Guid skuId, Guid orderId, int quantity) : base()
    {
        SkuId = skuId;
        OrderId = orderId;
        Quantity = quantity;
    }
}

/// <summary>
/// 库存释放集成事件，订单域在订单取消回退预占时发布。
/// 消费方：对账/审计域、未来跨上下文消费方。
/// 事件契约定义在共享层，变更需所有消费方协商。
/// </summary>
public sealed class StockReleasedIntegrationEvent : IntegrationEventBase
{
    /// <summary>SKU 标识（同时为聚合根标识）。</summary>
    public Guid SkuId { get; init; }

    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>释放数量。</summary>
    public int Quantity { get; init; }

    /// <summary>聚合根标识，用于发件箱归类。</summary>
    public Guid AggregateId => SkuId;

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public StockReleasedIntegrationEvent() : base()
    {
    }

    public StockReleasedIntegrationEvent(Guid skuId, Guid orderId, int quantity) : base()
    {
        SkuId = skuId;
        OrderId = orderId;
        Quantity = quantity;
    }
}
