using Leno.Inventory.Domain.Repositories;
using Leno.SharedContracts.Events;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Microsoft.Extensions.Logging;

namespace Leno.Inventory.Infrastructure.Consumers;

/// <summary>
/// 库存调整事件消费者（Product BC → Inventory BC）—— **基线的唯一写入方**。
/// <para>
/// 消费 Product BC 发布的 <see cref="StockAdjustedEvent"/>（商品域库存调整的权威值），
/// 经 <see cref="IStockBaselineRepository.SetAvailableAsync"/> 同步基线（不存在则创建）。
/// 双轨下线 DEC-4（2026-09-22）：Order BC 的同名消费者已删除 —— 此前两个 BC 各自消费本事件
/// 写同一批存储，属双写冲突；现在基线只有本消费者一个写入方。
/// 通过 EventId 幂等去重（继承 <see cref="IntegrationEventConsumerBase{T}"/>）。
/// </para>
/// </summary>
public sealed class StockAdjustedEventConsumer : IntegrationEventConsumerBase<StockAdjustedEvent>
{
    private readonly IStockBaselineRepository _baselineRepository;

    public StockAdjustedEventConsumer(
        IStockBaselineRepository baselineRepository,
        ILogger<StockAdjustedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(baselineRepository);
        _baselineRepository = baselineRepository;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(StockAdjustedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _baselineRepository.SetAvailableAsync(
            integrationEvent.SkuId,
            integrationEvent.ProductId,
            integrationEvent.AvailableQty,
            ct).ConfigureAwait(false);

        Logger.LogInformation("库存基线已同步 SkuId={SkuId} AvailableQty={AvailableQty}",
            integrationEvent.SkuId, integrationEvent.AvailableQty);
    }
}
