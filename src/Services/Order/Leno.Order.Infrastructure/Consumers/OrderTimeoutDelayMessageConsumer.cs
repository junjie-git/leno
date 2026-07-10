using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 订单超时延迟消息，由延迟队列在支付截止时间后投递。
/// 非 IIntegrationEvent，为普通 MassTransit 消息。
/// </summary>
public record OrderTimeoutMessage(Guid OrderId);

/// <summary>
/// 订单超时延迟消息消费者，检查待支付订单是否已超时，超时则自动取消。
/// 非 IntegrationEventConsumerBase，因 OrderTimeoutMessage 不是 IIntegrationEvent。
/// </summary>
public sealed class OrderTimeoutDelayMessageConsumer : IConsumer<OrderTimeoutMessage>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderTimeoutDelayMessageConsumer> _logger;

    public OrderTimeoutDelayMessageConsumer(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<OrderTimeoutDelayMessageConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderTimeoutMessage> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var msg = context.Message;

        var order = await _orderRepository.GetByIdAsync(msg.OrderId, context.CancellationToken);
        if (order is null)
        {
            _logger.LogInformation("超时取消：订单不存在 OrderId={OrderId}，跳过", msg.OrderId);
            return;
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            _logger.LogInformation("超时取消：订单 {OrderId} 当前状态 {Status} 非待支付，跳过",
                msg.OrderId, order.Status);
            return;
        }

        if (DateTime.UtcNow < order.ExpireAt)
        {
            _logger.LogInformation("超时取消：订单 {OrderId} 尚未到达支付截止时间 {ExpireAt}，跳过",
                msg.OrderId, order.ExpireAt);
            return;
        }

        order.Cancel("支付超时自动取消", "System");

        await _orderRepository.UpdateAsync(order, context.CancellationToken);
        await _unitOfWork.SaveEntitiesAsync(context.CancellationToken);

        _logger.LogInformation("订单 {OrderId} 因支付超时已自动取消", msg.OrderId);
    }
}
