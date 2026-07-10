using Leno.Infrastructure.EventBus;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 支付成功事件消费者，将待支付订单标记为已支付。
/// 通过 EventId 幂等去重；订单非待支付态时跳过（已处理或状态非法）。
/// </summary>
public sealed class PaymentSucceededEventConsumer : IntegrationEventConsumerBase<PaymentSucceededEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentSucceededEventConsumer(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<PaymentSucceededEventConsumer> logger)
        : base(logger)
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

        order.MarkAsPaid(integrationEvent.PaymentId, integrationEvent.Channel, integrationEvent.PaidAt, integrationEvent.TradeNo);

        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单 {OrderId} 已标记支付成功 PaymentId={PaymentId} TradeNo={TradeNo}",
            integrationEvent.OrderId, integrationEvent.PaymentId, integrationEvent.TradeNo);
    }
}
