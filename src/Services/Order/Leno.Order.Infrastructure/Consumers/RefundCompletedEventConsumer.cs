using Leno.Infrastructure.EventBus;
using Leno.Order.Domain.Repositories;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 退款完成事件消费者，释放已退款订单的预占库存。
/// 通过 EventId 幂等去重；订单不存在时跳过。
/// </summary>
public sealed class RefundCompletedEventConsumer : IntegrationEventConsumerBase<RefundCompletedEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public RefundCompletedEventConsumer(
        IOrderRepository orderRepository,
        IInventoryRepository inventoryRepository,
        ILogger<RefundCompletedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(inventoryRepository);
        _orderRepository = orderRepository;
        _inventoryRepository = inventoryRepository;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(RefundCompletedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var order = await _orderRepository.GetByIdAsync(integrationEvent.OrderId, ct);
        if (order is null)
        {
            Logger.LogInformation("退款完成事件：订单不存在 OrderId={OrderId}，跳过", integrationEvent.OrderId);
            return;
        }

        // 逐明细释放预占库存（退款回滚）
        foreach (var item in order.Items)
        {
            await _inventoryRepository.ReleaseAsync(item.SkuId, integrationEvent.OrderId, item.Quantity, ct);
        }

        Logger.LogInformation("退款完成：订单 {OrderId} 已释放 {ItemCount} 项预占库存 RefundId={RefundId}",
            integrationEvent.OrderId, order.Items.Count, integrationEvent.RefundId);
    }
}
