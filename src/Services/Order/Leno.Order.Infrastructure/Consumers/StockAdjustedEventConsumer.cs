using Leno.Infrastructure.EventBus;
using Leno.Order.Domain.Repositories;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 库存调整事件消费者，同步商品域可用库存到订单域 Redis 库存基线。
/// 通过 EventId 幂等去重。
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
