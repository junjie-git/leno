using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 支付成功事件消费者，仅负责将待支付订单标记为已支付（订单状态变更）与本地事务保存。
/// 库存确认（预占 → 真实扣减）与积分确认（冻结 → 正式扣减）已拆分至
/// <see cref="StockConfirmConsumer"/> 与 <see cref="PointsConfirmConsumer"/> 独立消费，
/// 三者通过独立队列 + 独立幂等键隔离，任一操作失败不影响其他已成功的操作。
/// 通过 EventId 幂等去重；订单非待支付态时跳过（已处理或状态非法）。
/// </summary>
public sealed class PaymentSucceededEventConsumer : IntegrationEventConsumerBase<PaymentSucceededEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentSucceededEventConsumer(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<PaymentSucceededEventConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(PaymentSucceededEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var order = await _orderRepository.GetByIdAsync(integrationEvent.OrderId, ct);
        if (order is null)
        {
            Logger.LogInformation("支付成功事件：订单不存在 OrderId={OrderId}，跳过", integrationEvent.OrderId);
            return;
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            Logger.LogInformation("支付成功事件：订单 {OrderId} 当前状态 {Status} 非待支付，跳过",
                integrationEvent.OrderId, order.Status);
            return;
        }

        order.MarkAsPaid(integrationEvent.PaymentId, integrationEvent.Channel, integrationEvent.PaidAt, integrationEvent.TradeNo, integrationEvent.Amount);

        // 会员订阅订单支付后自动完成（无发货流程）
        if (order.OrderType == OrderType.Membership)
        {
            order.CompleteMembershipOrder();

            await _orderRepository.UpdateAsync(order, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            Logger.LogInformation("会员订单 {OrderId} 已支付并自动完成", integrationEvent.OrderId);
            return;
        }

        // 库存确认与积分确认由独立的 StockConfirmConsumer / PointsConfirmConsumer 异步处理，
        // 本消费者仅负责订单状态变更与本地事务保存（含 Outbox 领域事件持久化）。
        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单 {OrderId} 已标记支付成功 PaymentId={PaymentId} TradeNo={TradeNo}",
            integrationEvent.OrderId, integrationEvent.PaymentId, integrationEvent.TradeNo);
    }
}
