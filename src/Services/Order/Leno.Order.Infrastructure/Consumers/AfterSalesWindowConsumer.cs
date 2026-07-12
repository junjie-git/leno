using Leno.Order.Application.Messages;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 售后窗口结束延迟消息消费者，在售后窗口到期后关闭订单的售后窗口。
/// 非 IntegrationEventConsumerBase，因 AfterSalesWindowMessage 不是 IIntegrationEvent。
/// </summary>
public sealed class AfterSalesWindowConsumer : IConsumer<AfterSalesWindowMessage>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AfterSalesWindowConsumer> _logger;

    public AfterSalesWindowConsumer(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<AfterSalesWindowConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AfterSalesWindowMessage> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var msg = context.Message;

        var order = await _orderRepository.GetByIdAsync(msg.OrderId, context.CancellationToken);
        if (order is null)
        {
            _logger.LogInformation("售后窗口关闭：订单不存在 OrderId={OrderId}，跳过", msg.OrderId);
            return;
        }

        if (order.Status != OrderStatus.Completed)
        {
            _logger.LogInformation("售后窗口关闭：订单 {OrderId} 当前状态 {Status} 非已完成，跳过",
                msg.OrderId, order.Status);
            return;
        }

        order.CloseAfterSalesWindow();

        await _orderRepository.UpdateAsync(order, context.CancellationToken);
        await _unitOfWork.SaveEntitiesAsync(context.CancellationToken);

        _logger.LogInformation("订单 {OrderId} 售后窗口已关闭", msg.OrderId);
    }
}