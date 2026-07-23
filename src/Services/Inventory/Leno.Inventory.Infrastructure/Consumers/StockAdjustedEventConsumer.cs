using Leno.Infrastructure.Abstractions;
using Leno.Inventory.Domain.Repositories;
using Leno.SharedContracts.Events;
using Leno.Infrastructure.EventBus;
using Microsoft.Extensions.Logging;

namespace Leno.Inventory.Infrastructure.Consumers;

/// <summary>
/// 库存调整事件消费者（Product BC → Inventory BC）。
/// 消费 Product BC 发布的 <see cref="StockAdjustedEvent"/>，调用 <see cref="IInventoryRepository.SetBaseLineAsync"/>
/// 同步 Redis 可用库存基线与 StockReservation 聚合基线。
/// 双轨期：Product BC 仍持有只读投影，本消费者保证 Inventory BC 的基线与 Product BC 一致。
/// 通过 EventId 幂等去重（继承 <see cref="IntegrationEventConsumerBase{T}"/>）。
/// </summary>
public sealed class StockAdjustedEventConsumer : IntegrationEventConsumerBase<StockAdjustedEvent>
{
    private readonly IInventoryRepository _inventoryRepository;

    public StockAdjustedEventConsumer(
        IInventoryRepository inventoryRepository,
        ILogger<StockAdjustedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(inventoryRepository);
        _inventoryRepository = inventoryRepository;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(StockAdjustedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _inventoryRepository.SetBaseLineAsync(integrationEvent.SkuId, integrationEvent.AvailableQty, ct);

        Logger.LogInformation("库存基线已同步 SkuId={SkuId} AvailableQty={AvailableQty}",
            integrationEvent.SkuId, integrationEvent.AvailableQty);
    }
}
