using Leno.Infrastructure.EventBus;
using Leno.Inventory.Domain.Events;
using Leno.SharedContracts.Events;
using Leno.SharedContracts.Integration.Inventory;

namespace Leno.Inventory.Infrastructure.EventBus;

/// <summary>
/// Inventory BC 领域事件到集成事件的翻译器。
/// 翻译规则：
/// - <see cref="StockAdjustedDomainEvent"/> → <see cref="StockAdjustedEvent"/>（通知 Product BC 同步只读投影、对账/审计域对账）
/// - <see cref="CompensationMaxRetriesExceededDomainEvent"/> → <see cref="CompensationMaxRetriesExceededIntegrationEvent"/>（通知告警/运维域人工介入）
/// - <see cref="StockReservedEvent"/> / <see cref="StockConfirmedEvent"/> / <see cref="StockReleasedEvent"/>：不翻译，
///   由 <c>InventoryAppService</c> 直接通过 <c>IPublishEndpoint</c> 发布多 SKU 维度的
///   <see cref="StockReservedIntegrationEvent"/> / <see cref="StockConfirmedIntegrationEvent"/> / <see cref="StockReleasedIntegrationEvent"/>。
/// </summary>
public class InventoryIntegrationEventMapper : IntegrationEventMapperBase
{
    public InventoryIntegrationEventMapper()
    {
        // StockAdjustedDomainEvent → StockAdjustedEvent（Product BC 同步只读投影、对账/审计域对账）
        RegisterHandler<StockAdjustedDomainEvent, StockAdjustedEvent>(e =>
            new StockAdjustedEvent(e.SkuId, e.ProductId, e.AvailableQty, e.Delta, e.AdjustedAtUtc));

        // CompensationMaxRetriesExceededDomainEvent → CompensationMaxRetriesExceededIntegrationEvent（告警/运维域人工介入）
        RegisterHandler<CompensationMaxRetriesExceededDomainEvent, CompensationMaxRetriesExceededIntegrationEvent>(e =>
            new CompensationMaxRetriesExceededIntegrationEvent(
                e.CompensationId,
                e.OrderId,
                e.SkuId,
                e.Quantity,
                e.RetryCount,
                e.MaxRetries,
                e.LastErrorMessage,
                e.OccurredAtUtc));
    }
}
