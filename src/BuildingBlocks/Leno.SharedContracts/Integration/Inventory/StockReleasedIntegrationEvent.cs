using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Integration.Inventory;

/// <summary>
/// 库存释放成功集成事件（Inventory BC → Order BC）。
/// Inventory BC 完成 <see cref="ReleaseStockCommand"/> 后发布。
/// </summary>
public sealed class StockReleasedIntegrationEvent : IntegrationEventBase
{
    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>释放操作类型（Release 或 ReturnDeducted）。</summary>
    public ReleaseStockOperationType OperationType { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public StockReleasedIntegrationEvent() : base() { }

    public StockReleasedIntegrationEvent(Guid orderId, ReleaseStockOperationType operationType) : base()
    {
        OrderId = orderId;
        OperationType = operationType;
    }
}
