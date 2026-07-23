using Leno.Infrastructure.Abstractions;
using Leno.Order.Application.ProcessManagers;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 积分确认消费者，订阅支付成功事件，仅负责将订单冻结积分转为正式扣减（冻结 → 正式扣减）。
/// 独立于订单状态变更（<see cref="PaymentSucceededEventConsumer"/>）与库存确认（<see cref="StockConfirmConsumer"/>）：
/// 通过独立队列（order-points-confirm）与独立幂等键（points-confirm-{PaymentId}）实现隔离，
/// 任一操作失败不影响本消费者的执行结果与重试，反之亦然。
/// 会员订阅订单跳过积分确认（与原 PaymentSucceededEventConsumer 早期返回行为一致）。
/// 3.3：双轨期 feature flag <c>Order:UsePaymentProcessManager</c> 切流。
/// flag=true 时（shadow 模式）：消费者在完成积分确认后调用 <see cref="IOrderPaymentProcessManager.HandlePointsConfirmedAsync"/>
/// 转发完成回调给 Process Manager。旧路径仍执行实际工作以保证功能兼容。
/// flag=false 时：仅走旧路径，不调用 Process Manager。
/// </summary>
public sealed class PointsConfirmConsumer : IConsumer<PaymentSucceededEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPointsAntiCorruptionService _pointsAntiCorruption;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<PointsConfirmConsumer> _logger;
    private readonly IOrderPaymentProcessManager _processManager;
    private readonly IOptionsMonitor<OrderPaymentProcessOptions> _options;

    public PointsConfirmConsumer(
        IOrderRepository orderRepository,
        IPointsAntiCorruptionService pointsAntiCorruption,
        IIdempotencyStore idempotencyStore,
        ILogger<PointsConfirmConsumer> logger,
        IOrderPaymentProcessManager processManager,
        IOptionsMonitor<OrderPaymentProcessOptions> options)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(pointsAntiCorruption);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(processManager);
        ArgumentNullException.ThrowIfNull(options);

        _orderRepository = orderRepository;
        _pointsAntiCorruption = pointsAntiCorruption;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
        _processManager = processManager;
        _options = options;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<PaymentSucceededEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        var ct = context.CancellationToken;

        // 幂等键：基于 PaymentId + 操作类型，独立于订单状态消费者与库存消费者的幂等键，
        // 避免重试时重复执行积分正式扣减（积分域远程调用不应重复扣减）。
        var idempotencyId = IdempotencyKeyHelper.ToDeterministicGuid($"points-confirm-{evt.PaymentId}");

        if (await _idempotencyStore.IsProcessedAsync(idempotencyId, ct))
        {
            _logger.LogInformation("积分确认已处理，跳过重复消费 PaymentId={PaymentId} OrderId={OrderId}",
                evt.PaymentId, evt.OrderId);
            return;
        }

        // 原子获取处理权（若 store 支持 SET NX），消除并发穿透
        if (_idempotencyStore.SupportsAtomicProcessing
            && !await _idempotencyStore.TryMarkAsProcessingAsync(idempotencyId, ct))
        {
            _logger.LogInformation("积分确认被其他消费者占用或已处理，跳过 PaymentId={PaymentId}",
                evt.PaymentId);
            return;
        }

        var useProcessManager = OrderPaymentProcessRolloutEvaluator.ShouldUseProcessManager(
            _options.CurrentValue, evt.OrderId);

        try
        {
            await ConfirmPointsAsync(evt, ct);
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
        _logger.LogInformation("积分确认完成 PaymentId={PaymentId} OrderId={OrderId}",
            evt.PaymentId, evt.OrderId);

        // 3.3 双轨期 shadow 模式：转发积分确认完成回调给 Process Manager
        if (useProcessManager)
        {
            await TryHandlePointsConfirmedAsync(evt.OrderId, ct);
        }
    }

    /// <summary>
    /// 加载订单并调用积分防腐层确认扣减。会员订阅订单跳过。
    /// </summary>
    private async Task ConfirmPointsAsync(PaymentSucceededEvent evt, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(evt.OrderId, ct);
        if (order is null)
        {
            _logger.LogInformation("积分确认：订单不存在 OrderId={OrderId}，跳过", evt.OrderId);
            return;
        }

        // 会员订阅订单跳过积分确认（与原 PaymentSucceededEventConsumer 早期返回行为一致）
        if (order.OrderType == OrderType.Membership)
        {
            _logger.LogDebug("积分确认：会员订单 {OrderId} 跳过积分确认", evt.OrderId);
            return;
        }

        await _pointsAntiCorruption.ConfirmDeductionAsync(order.Id, ct);
    }

    /// <summary>
    /// 转发积分确认完成回调给 Process Manager。
    /// 异常隔离：回调失败不应影响旧路径的实际工作（shadow 模式），仅记录错误日志。
    /// </summary>
    private async Task TryHandlePointsConfirmedAsync(Guid orderId, CancellationToken ct)
    {
        try
        {
            await _processManager.HandlePointsConfirmedAsync(orderId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Process Manager HandlePointsConfirmedAsync 回调失败，不影响旧路径实际工作 OrderId={OrderId}",
                orderId);
        }
    }
}
