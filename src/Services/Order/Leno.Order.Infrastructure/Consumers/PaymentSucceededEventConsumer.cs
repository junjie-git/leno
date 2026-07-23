using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Order.Application.ProcessManagers;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 支付成功事件消费者，仅负责将待支付订单标记为已支付（订单状态变更）与本地事务保存。
/// 库存确认（预占 → 真实扣减）与积分确认（冻结 → 正式扣减）已拆分至
/// <see cref="StockConfirmConsumer"/> 与 <see cref="PointsConfirmConsumer"/> 独立消费，
/// 三者通过独立队列 + 独立幂等键隔离，任一操作失败不影响其他已成功的操作。
/// 通过 EventId 幂等去重；订单非待支付态时跳过（已处理或状态非法）。
/// 3.3：双轨期 feature flag <c>Order:UsePaymentProcessManager</c> 切流。
/// flag=true 时（shadow 模式）：消费者在完成 MarkAsPaid 后调用 <see cref="IOrderPaymentProcessManager.HandleOrderMarkedPaidAsync"/>
/// 转发完成回调给 Process Manager，跟踪三个子任务完成度并发布编排事件。旧路径仍执行实际工作以保证功能兼容。
/// flag=false 时：仅走旧路径，不调用 Process Manager。
/// </summary>
public sealed class PaymentSucceededEventConsumer : IntegrationEventConsumerBase<PaymentSucceededEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderPaymentProcessManager _processManager;
    private readonly IOptionsMonitor<OrderPaymentProcessOptions> _options;

    public PaymentSucceededEventConsumer(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<PaymentSucceededEventConsumer> logger,
        IIdempotencyStore idempotencyStore,
        IOrderPaymentProcessManager processManager,
        IOptionsMonitor<OrderPaymentProcessOptions> options)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(processManager);
        ArgumentNullException.ThrowIfNull(options);

        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _processManager = processManager;
        _options = options;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(PaymentSucceededEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var useProcessManager = OrderPaymentProcessRolloutEvaluator.ShouldUseProcessManager(
            _options.CurrentValue, integrationEvent.OrderId);

        // 3.3 双轨期 shadow 模式：在执行 MarkAsPaid 前启动 Process Manager（创建状态记录、发布编排事件与子任务命令）
        // 先启动可最小化与其他消费者（Stock/Points）完成回调的竞态窗口
        if (useProcessManager)
        {
            await TryStartProcessManagerAsync(integrationEvent, ct);
        }

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

            // 3.3 双轨期 shadow 模式：转发 MarkOrderPaid 完成回调给 Process Manager
            if (useProcessManager)
            {
                await TryHandleOrderMarkedPaidAsync(integrationEvent.OrderId, ct);
            }

            Logger.LogInformation("会员订单 {OrderId} 已支付并自动完成", integrationEvent.OrderId);
            return;
        }

        // 库存确认与积分确认由独立的 StockConfirmConsumer / PointsConfirmConsumer 异步处理，
        // 本消费者仅负责订单状态变更与本地事务保存（含 Outbox 领域事件持久化）。
        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 3.3 双轨期 shadow 模式：转发 MarkOrderPaid 完成回调给 Process Manager
        if (useProcessManager)
        {
            await TryHandleOrderMarkedPaidAsync(integrationEvent.OrderId, ct);
        }

        Logger.LogInformation("订单 {OrderId} 已标记支付成功 PaymentId={PaymentId} TradeNo={TradeNo}",
            integrationEvent.OrderId, integrationEvent.PaymentId, integrationEvent.TradeNo);
    }

    /// <summary>
    /// 启动 Process Manager（创建状态记录、发布编排事件与子任务命令）。
    /// 异常隔离：Process Manager 启动失败不应影响旧路径的实际工作（shadow 模式），
    /// 仅记录错误日志，由监控告警捕获。
    /// </summary>
    private async Task TryStartProcessManagerAsync(PaymentSucceededEvent evt, CancellationToken ct)
    {
        try
        {
            await _processManager.StartAsync(
                evt.OrderId,
                evt.PaymentId,
                evt.Channel,
                evt.TradeNo,
                evt.Amount,
                evt.Currency,
                evt.PaidAt,
                ct);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex,
                "Process Manager 启动失败，不影响旧路径实际工作 OrderId={OrderId} PaymentId={PaymentId}",
                evt.OrderId, evt.PaymentId);
        }
    }

    /// <summary>
    /// 转发 MarkOrderPaid 完成回调给 Process Manager。
    /// 异常隔离：回调失败不应影响旧路径的实际工作（shadow 模式），仅记录错误日志。
    /// </summary>
    private async Task TryHandleOrderMarkedPaidAsync(Guid orderId, CancellationToken ct)
    {
        try
        {
            await _processManager.HandleOrderMarkedPaidAsync(orderId, ct);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex,
                "Process Manager HandleOrderMarkedPaidAsync 回调失败，不影响旧路径实际工作 OrderId={OrderId}",
                orderId);
        }
    }
}
