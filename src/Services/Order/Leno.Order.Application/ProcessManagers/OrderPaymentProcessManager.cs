using Leno.Order.Application.ProcessManagers.Commands;
using Leno.Order.Application.ProcessManagers.Events;
using Leno.Order.Application.ProcessManagers.States;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Application.ProcessManagers;

/// <summary>
/// 订单支付流程编排器（Process Manager）实现，编排支付成功后三个并行子任务
/// （<c>MarkOrderPaid</c> / <c>ConfirmStock</c> / <c>ConfirmPoints</c>）的整体完成状态。
/// 这是 Saga 状态机之上的业务编排层：<see cref="Leno.Order.Application.Sagas.OrderSagaStateMachine"/> 编排下单全流程
/// （库存预占 → 积分冻结 → 订单创建 → 支付 → 完成），本编排器仅负责支付成功后的子任务跟踪与反向补偿。
/// </summary>
/// <remarks>
/// <para>
/// 双轨期（<see cref="OrderPaymentProcessOptions.UsePaymentProcessManager"/>=true，shadow 模式）：
/// 三个消费者（<c>PaymentSucceededEventConsumer</c> / <c>StockConfirmConsumer</c> / <c>PointsConfirmConsumer</c>）
/// 在完成各自的旧路径实际工作后，将完成回调转发给本编排器；
/// 本编排器创建 <see cref="OrderPaymentProcessState"/> 状态记录，跟踪三个子任务完成度，
/// 全部完成后发布 <see cref="OrderPaymentProcessCompleted"/> 事件，供未来全量切流的下游消费者订阅。
/// </para>
/// <para>
/// 乐观锁：三个子任务可能并发回写状态，由 EF Core 检测 <see cref="OrderPaymentProcessState.RowVersion"/> 冲突并重试。
/// 幂等：<see cref="StartAsync"/> 通过 <c>GetByOrderIdAsync</c> 检查是否已存在，避免重复创建与重复发布命令；
/// <see cref="HandleStockConfirmedAsync"/> 等回调方法通过检查已完成标志避免重复处理。
/// </para>
/// <para>
/// 状态流转：AwaitingStockConfirm → AwaitingPointsConfirm → AwaitingMarkPaid → Completed
/// （中间状态名仅用于观测，三个标志可乱序完成）；任一失败 → Compensating → Compensated。
/// </para>
/// </remarks>
public sealed class OrderPaymentProcessManager : IOrderPaymentProcessManager
{
    /// <summary>状态名称常量，与 <see cref="OrderPaymentProcessState.CurrentState"/> 持久化值一致。</summary>
    public static class StateNames
    {
        public const string AwaitingStockConfirm = "AwaitingStockConfirm";
        public const string AwaitingPointsConfirm = "AwaitingPointsConfirm";
        public const string AwaitingMarkPaid = "AwaitingMarkPaid";
        public const string Completed = "Completed";
        public const string Compensating = "Compensating";
        public const string Compensated = "Compensated";
    }

    /// <summary>子任务名称常量，与 <see cref="HandleSubTaskFailedAsync"/> 的 <paramref name="subTask"/> 参数对齐。</summary>
    public static class SubTaskNames
    {
        public const string Stock = "Stock";
        public const string Points = "Points";
        public const string MarkOrderPaid = "MarkOrderPaid";
    }

    private readonly IOrderPaymentProcessRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBus _bus;
    private readonly ILogger<OrderPaymentProcessManager> _logger;

    public OrderPaymentProcessManager(
        IOrderPaymentProcessRepository repository,
        IUnitOfWork unitOfWork,
        IBus bus,
        ILogger<OrderPaymentProcessManager> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _unitOfWork = unitOfWork;
        _bus = bus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OrderPaymentProcessState> StartAsync(
        Guid orderId,
        Guid paymentId,
        string paymentChannel,
        string tradeNo,
        decimal amount,
        string currency,
        DateTime paidAt,
        CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId 不能为 Guid.Empty", nameof(orderId));
        }
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("PaymentId 不能为 Guid.Empty", nameof(paymentId));
        }

        // 幂等：同一订单仅创建一次流程状态，重复调用返回已存在的状态且不重复发布命令
        var existing = await _repository.GetByOrderIdAsync(orderId, ct);
        if (existing is not null)
        {
            _logger.LogInformation(
                "支付流程已存在，跳过重复启动 OrderId={OrderId} ProcessId={ProcessId} CurrentState={State}",
                orderId, existing.ProcessId, existing.CurrentState);
            return existing;
        }

        var now = DateTime.UtcNow;
        var state = new OrderPaymentProcessState
        {
            ProcessId = Guid.NewGuid(),
            OrderId = orderId,
            PaymentId = paymentId,
            CurrentState = StateNames.AwaitingStockConfirm,
            StockConfirmed = false,
            PointsConfirmed = false,
            OrderMarkedPaid = false,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = Array.Empty<byte>()
        };

        await _repository.SaveAsync(state, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation(
            "支付流程已启动 OrderId={OrderId} PaymentId={PaymentId} ProcessId={ProcessId} Channel={Channel} TradeNo={TradeNo} Amount={Amount} {Currency} PaidAt={PaidAt}",
            orderId, paymentId, state.ProcessId, paymentChannel, tradeNo, amount, currency, paidAt);

        // 发布编排启动事件，通知下游 Process Manager 已启动
        await _bus.Publish(new OrderPaymentProcessStarted(orderId, paymentId, state.ProcessId), ct);

        // 并行发布三个子任务命令（双轨期作为编排信号发布；旧路径消费者仍直接消费 PaymentSucceededEvent 执行实际工作）
        await _bus.Publish(new ConfirmStockCommand(orderId, paymentId), ct);
        await _bus.Publish(new ConfirmPointsCommand(orderId, paymentId), ct);
        await _bus.Publish(new MarkOrderPaidCommand(orderId, paymentId), ct);

        return state;
    }

    /// <inheritdoc />
    public async Task HandleStockConfirmedAsync(Guid orderId, CancellationToken ct = default)
    {
        var state = await LoadStateOrWarnAsync(orderId, nameof(HandleStockConfirmedAsync), ct);
        if (state is null)
        {
            return;
        }

        if (state.StockConfirmed)
        {
            _logger.LogDebug("库存确认已记录，幂等跳过 OrderId={OrderId}", orderId);
            return;
        }

        if (IsTerminalOrCompensating(state))
        {
            _logger.LogWarning(
                "流程处于终态/补偿中，忽略库存确认回调 OrderId={OrderId} State={State}",
                orderId, state.CurrentState);
            return;
        }

        state.StockConfirmed = true;
        state.UpdatedAt = DateTime.UtcNow;
        await TryCompleteAsync(state, ct);
    }

    /// <inheritdoc />
    public async Task HandlePointsConfirmedAsync(Guid orderId, CancellationToken ct = default)
    {
        var state = await LoadStateOrWarnAsync(orderId, nameof(HandlePointsConfirmedAsync), ct);
        if (state is null)
        {
            return;
        }

        if (state.PointsConfirmed)
        {
            _logger.LogDebug("积分确认已记录，幂等跳过 OrderId={OrderId}", orderId);
            return;
        }

        if (IsTerminalOrCompensating(state))
        {
            _logger.LogWarning(
                "流程处于终态/补偿中，忽略积分确认回调 OrderId={OrderId} State={State}",
                orderId, state.CurrentState);
            return;
        }

        state.PointsConfirmed = true;
        state.UpdatedAt = DateTime.UtcNow;
        await TryCompleteAsync(state, ct);
    }

    /// <inheritdoc />
    public async Task HandleOrderMarkedPaidAsync(Guid orderId, CancellationToken ct = default)
    {
        var state = await LoadStateOrWarnAsync(orderId, nameof(HandleOrderMarkedPaidAsync), ct);
        if (state is null)
        {
            return;
        }

        if (state.OrderMarkedPaid)
        {
            _logger.LogDebug("订单标记已支付已记录，幂等跳过 OrderId={OrderId}", orderId);
            return;
        }

        if (IsTerminalOrCompensating(state))
        {
            _logger.LogWarning(
                "流程处于终态/补偿中，忽略订单标记已支付回调 OrderId={OrderId} State={State}",
                orderId, state.CurrentState);
            return;
        }

        state.OrderMarkedPaid = true;
        state.UpdatedAt = DateTime.UtcNow;
        await TryCompleteAsync(state, ct);
    }

    /// <inheritdoc />
    public async Task TryCompleteAsync(OrderPaymentProcessState state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.StockConfirmed && state.PointsConfirmed && state.OrderMarkedPaid)
        {
            state.CurrentState = StateNames.Completed;
            state.UpdatedAt = DateTime.UtcNow;

            await _repository.SaveAsync(state, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            await _bus.Publish(
                new OrderPaymentProcessCompleted(state.OrderId, state.PaymentId, state.ProcessId, DateTime.UtcNow),
                ct);

            _logger.LogInformation(
                "支付流程已完成 OrderId={OrderId} ProcessId={ProcessId}",
                state.OrderId, state.ProcessId);
        }
        else
        {
            state.CurrentState = DeriveIntermediateState(state);
            state.UpdatedAt = DateTime.UtcNow;

            await _repository.SaveAsync(state, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            _logger.LogDebug(
                "支付流程状态更新 OrderId={OrderId} State={State} StockConfirmed={Stock} PointsConfirmed={Points} OrderMarkedPaid={MarkPaid}",
                state.OrderId, state.CurrentState, state.StockConfirmed, state.PointsConfirmed, state.OrderMarkedPaid);
        }
    }

    /// <inheritdoc />
    public async Task HandleSubTaskFailedAsync(Guid orderId, string subTask, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subTask);

        var state = await LoadStateOrWarnAsync(orderId, nameof(HandleSubTaskFailedAsync), ct);
        if (state is null)
        {
            return;
        }

        // 已终结状态幂等跳过（避免重复补偿）
        if (state.CurrentState is StateNames.Completed or StateNames.Compensated)
        {
            _logger.LogInformation(
                "流程已终结，忽略子任务失败回调 OrderId={OrderId} State={State} SubTask={SubTask}",
                orderId, state.CurrentState, subTask);
            return;
        }

        state.CurrentState = StateNames.Compensating;
        state.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveAsync(state, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        await _bus.Publish(
            new OrderPaymentProcessCompensating(orderId, state.ProcessId, subTask, "SubTaskFailed"),
            ct);

        _logger.LogWarning(
            "支付流程进入补偿 OrderId={OrderId} ProcessId={ProcessId} FailedSubTask={SubTask}",
            orderId, state.ProcessId, subTask);

        // 对已完成的子任务发布反向补偿命令（仅补偿已完成的，避免对未执行的操作做反向补偿）
        if (state.StockConfirmed)
        {
            await _bus.Publish(new CompensateStockConfirmCommand(orderId, state.PaymentId), ct);
            _logger.LogInformation("已发布库存反向补偿命令 OrderId={OrderId}", orderId);
        }
        if (state.PointsConfirmed)
        {
            await _bus.Publish(new CompensatePointsConfirmCommand(orderId, state.PaymentId), ct);
            _logger.LogInformation("已发布积分反向补偿命令 OrderId={OrderId}", orderId);
        }
        if (state.OrderMarkedPaid)
        {
            await _bus.Publish(new CompensateMarkOrderPaidCommand(orderId, state.PaymentId), ct);
            _logger.LogInformation("已发布订单标记已支付反向补偿命令 OrderId={OrderId}", orderId);
        }

        state.CurrentState = StateNames.Compensated;
        state.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveAsync(state, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogWarning(
            "支付流程补偿完成 OrderId={OrderId} ProcessId={ProcessId} FailedSubTask={SubTask}",
            orderId, state.ProcessId, subTask);
    }

    /// <summary>
    /// 加载流程状态，若不存在则记录警告并返回 null。
    /// 双轨期 shadow 模式下，可能存在消费者先于 <see cref="StartAsync"/> 完成子任务的竞态：
    /// 此时 <see cref="HandleStockConfirmedAsync"/> 等回调会因状态未创建而跳过，
    /// Process Manager 状态记录保持中间态，由监控告警捕获（不影响旧路径实际工作）。
    /// </summary>
    private async Task<OrderPaymentProcessState?> LoadStateOrWarnAsync(
        Guid orderId, string operationName, CancellationToken ct)
    {
        var state = await _repository.GetByOrderIdAsync(orderId, ct);
        if (state is null)
        {
            _logger.LogWarning(
                "{Operation} 回调：流程状态不存在 OrderId={OrderId}，可能存在竞态（消费者先于 StartAsync 完成），跳过",
                operationName, orderId);
        }
        return state;
    }

    /// <summary>
    /// 根据三个完成标志推导中间状态名（仅用于观测，三个标志可乱序完成）。
    /// 完成数 0 → AwaitingStockConfirm；1 → AwaitingPointsConfirm；2 → AwaitingMarkPaid。
    /// </summary>
    private static string DeriveIntermediateState(OrderPaymentProcessState state)
    {
        var completedCount =
            (state.StockConfirmed ? 1 : 0) +
            (state.PointsConfirmed ? 1 : 0) +
            (state.OrderMarkedPaid ? 1 : 0);

        return completedCount switch
        {
            0 => StateNames.AwaitingStockConfirm,
            1 => StateNames.AwaitingPointsConfirm,
            2 => StateNames.AwaitingMarkPaid,
            _ => StateNames.Completed
        };
    }

    /// <summary>
    /// 判断状态是否为终态或补偿中，回调时跳过避免污染已终结流程。
    /// </summary>
    private static bool IsTerminalOrCompensating(OrderPaymentProcessState state)
        => state.CurrentState is StateNames.Completed
            or StateNames.Compensating
            or StateNames.Compensated;
}
