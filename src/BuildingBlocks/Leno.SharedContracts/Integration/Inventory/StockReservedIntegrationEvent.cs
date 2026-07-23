using Leno.SharedContracts.Events;

namespace Leno.SharedContracts.Integration.Inventory;

/// <summary>
/// 库存预占成功集成事件（Inventory BC → Order BC）。
/// Inventory BC 完成 <see cref="ReserveStockCommand"/> 后发布，Order Saga 据此推进状态机。
/// </summary>
public sealed class StockReservedIntegrationEvent : IntegrationEventBase
{
    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>本次预占成功的 SKU 与数量列表。</summary>
    public IReadOnlyList<ReservedSkuItem> ReservationItems { get; init; } = Array.Empty<ReservedSkuItem>();

    /// <summary>预占过期时间（UTC），超时未确认由 Inventory BC 主动释放。</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>供 System.Text.Json 反序列化使用的无参构造。</summary>
    public StockReservedIntegrationEvent() : base() { }

    public StockReservedIntegrationEvent(
        Guid orderId,
        IReadOnlyList<ReservedSkuItem> reservationItems,
        DateTime? expiresAt) : base()
    {
        OrderId = orderId;
        ReservationItems = reservationItems;
        ExpiresAt = expiresAt;
    }
}

/// <summary>
/// 预占 SKU 明细项。
/// </summary>
public sealed record ReservedSkuItem(Guid SkuId, int Quantity, long SellerId);
