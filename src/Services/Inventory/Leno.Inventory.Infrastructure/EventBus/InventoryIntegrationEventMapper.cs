using Leno.Infrastructure.EventBus;
using Leno.Inventory.Domain.Events;
using Leno.SharedContracts.Events;
using Leno.SharedContracts.Integration.Inventory;

namespace Leno.Inventory.Infrastructure.EventBus;

/// <summary>
/// Inventory BC 领域事件到集成事件的翻译器。
/// <para>
/// 双轨下线 DEC-4（2026-09-22）后仅保留一条翻译规则：
/// <see cref="StockAdjustedDomainEvent"/> → <see cref="StockAdjustedEvent"/>（卖家补货/盘点调整，
/// 通知 Product BC 同步只读投影）。
/// </para>
/// <para>
/// 已删除：CompensationMaxRetriesExceeded 翻译（补偿机器随双轨下线，可靠性由消息重试 + DLQ 承担）。
/// 预占/确认/释放回执事件由 <c>InventoryAppService</c> 提交事务后经 <c>IPublishEndpoint</c> 直接发布
/// （订单 × SKU 多条目维度），不经过领域事件翻译。
/// </para>
/// </summary>
public class InventoryIntegrationEventMapper : IntegrationEventMapperBase
{
    public InventoryIntegrationEventMapper()
    {
        // StockAdjustedDomainEvent → StockAdjustedEvent（Product BC 同步只读投影）
        RegisterHandler<StockAdjustedDomainEvent, StockAdjustedEvent>(e =>
            new StockAdjustedEvent(e.SkuId, e.ProductId, e.AvailableQty, e.Delta, e.AdjustedAtUtc));
    }
}
