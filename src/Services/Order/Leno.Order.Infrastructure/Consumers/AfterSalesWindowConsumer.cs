using Leno.Infrastructure.Abstractions;
using Leno.Order.Application.Messages;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 售后窗口结束延迟消息消费者，在售后窗口到期后关闭订单的售后窗口。
/// 非 IntegrationEventConsumerBase，因 AfterSalesWindowMessage 不是 IIntegrationEvent，
/// 故通过 <see cref="IIdempotencyStore"/> 基于 <c>aftersales-window-{OrderId}</c> 幂等键独立去重，
/// 避免延迟消息重投递导致重复关闭售后窗口。
/// </summary>
public sealed class AfterSalesWindowConsumer : IConsumer<AfterSalesWindowMessage>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<AfterSalesWindowConsumer> _logger;

    public AfterSalesWindowConsumer(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IIdempotencyStore idempotencyStore,
        ILogger<AfterSalesWindowConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        ArgumentNullException.ThrowIfNull(logger);
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AfterSalesWindowMessage> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var msg = context.Message;
        var ct = context.CancellationToken;

        // 幂等键：基于 OrderId 唯一标识售后窗口关闭操作，避免延迟消息重投递导致重复关闭。
        var idempotencyId = IdempotencyKeyHelper.ToDeterministicGuid($"aftersales-window-{msg.OrderId}");

        if (await _idempotencyStore.IsProcessedAsync(idempotencyId, ct))
        {
            _logger.LogInformation("售后窗口关闭已处理，跳过重复消费 OrderId={OrderId}", msg.OrderId);
            return;
        }

        // 原子获取处理权（若 store 支持 SET NX），消除并发穿透
        if (_idempotencyStore.SupportsAtomicProcessing
            && !await _idempotencyStore.TryMarkAsProcessingAsync(idempotencyId, ct))
        {
            _logger.LogInformation("售后窗口关闭被其他消费者占用或已处理，跳过 OrderId={OrderId}", msg.OrderId);
            return;
        }

        try
        {
            await CloseAfterSalesWindowAsync(msg, ct);
        }
        catch
        {
            // 处理失败：释放处理锁，允许 MassTransit 后续重试
            if (_idempotencyStore.SupportsAtomicProcessing)
            {
                await _idempotencyStore.ReleaseProcessingLockAsync(idempotencyId, ct);
            }
            throw;
        }

        await _idempotencyStore.MarkAsProcessedAsync(idempotencyId, ct);
        _logger.LogInformation("订单 {OrderId} 售后窗口关闭处理完成", msg.OrderId);
    }

    /// <summary>
    /// 加载订单并校验状态后关闭售后窗口，持久化订单状态变更。
    /// </summary>
    private async Task CloseAfterSalesWindowAsync(AfterSalesWindowMessage msg, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(msg.OrderId, ct);
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

        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation("订单 {OrderId} 售后窗口已关闭", msg.OrderId);
    }
}
