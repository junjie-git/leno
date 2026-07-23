using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Integration.Inventory;

/// <summary>
/// 库存确认扣减成功集成事件（Inventory BC → Order BC）。
/// Inventory BC 完成 <see cref="ConfirmStockCommand"/> 后发布。
/// </summary>
public sealed class StockConfirmedIntegrationEvent : IntegrationEventBase
{
    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public StockConfirmedIntegrationEvent() : base() { }

    public StockConfirmedIntegrationEvent(Guid orderId) : base()
    {
        OrderId = orderId;
    }
}
