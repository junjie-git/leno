using Leno.Infrastructure.EventBus;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 退款完成事件消费者，按订单当前状态选择归还已扣减库存或释放预占库存。
/// 通过 EventId 幂等去重；订单不存在时跳过。
/// </summary>
public sealed class RefundCompletedEventConsumer : IntegrationEventConsumerBase<RefundCompletedEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IStockReservationDomainService _stockReservationDomainService;

    public RefundCompletedEventConsumer(
        IOrderRepository orderRepository,
        IStockReservationDomainService stockReservationDomainService,
        ILogger<RefundCompletedEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(stockReservationDomainService);
        _orderRepository = orderRepository;
        _stockReservationDomainService = stockReservationDomainService;
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

        // 按订单当前状态选择归还已扣减或释放预占
        // Paid/Shipped 状态：库存已被确认扣减（ConfirmBatchAsync），需归还已扣减库存
        // PendingPayment 状态：库存仍为预占，释放预占即可
        var needsReturnDeducted = order.Status == OrderStatus.Paid || order.Status == OrderStatus.Shipped;
        var skuQuantities = order.Items
            .GroupBy(i => i.SkuId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        if (needsReturnDeducted)
        {
            await _stockReservationDomainService.ReturnDeductedBatchAsync(order.Id, skuQuantities, ct);
            Logger.LogInformation("退款完成：订单 {OrderId} 已归还已扣减库存 {ItemCount} 项 RefundId={RefundId}",
                integrationEvent.OrderId, order.Items.Count, integrationEvent.RefundId);
        }
        else
        {
            await _stockReservationDomainService.ReleaseBatchAsync(order.Id, skuQuantities, ct);
            Logger.LogInformation("退款完成：订单 {OrderId} 已释放预占库存 {ItemCount} 项 RefundId={RefundId}",
                integrationEvent.OrderId, order.Items.Count, integrationEvent.RefundId);
        }
    }
}
