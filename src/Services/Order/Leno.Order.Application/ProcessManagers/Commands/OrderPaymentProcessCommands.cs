using MassTransit;

namespace Leno.Order.Application.ProcessManagers.Commands;

/// <summary>
/// 订单支付流程编排子任务命令与反向补偿命令契约。
/// 由 <see cref="OrderPaymentProcessManager"/> 在 <c>StartAsync</c> / <c>HandleSubTaskFailedAsync</c> 时发布。
/// 双轨期：三个子任务命令（<see cref="ConfirmStockCommand"/> / <see cref="ConfirmPointsCommand"/> /
/// <see cref="MarkOrderPaidCommand"/>）作为编排信号发布，供未来全量切流的命令消费者订阅；
/// 当前双轨期实际子任务工作仍由直接消费 <c>PaymentSucceededEvent</c> 的消费者执行，
/// Process Manager 通过 <c>Handle*Async</c> 回调跟踪完成度。
/// </summary>

/// <summary>
/// 库存确认子任务启动命令（Process Manager → 子任务消费者）。
/// 由 <see cref="OrderPaymentProcessManager.StartAsync"/> 并行发布，触发库存确认子任务。
/// 与跨 BC 的 <c>Leno.SharedContracts.Integration.Inventory.ConfirmStockCommand</c>（Order→Inventory）不同，
/// 本命令是 Order BC 内部编排信号，命名空间隔离避免冲突。
/// </summary>
public sealed record ConfirmStockCommand(
    Guid OrderId,
    Guid PaymentId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 积分确认子任务启动命令（Process Manager → 子任务消费者）。
/// 由 <see cref="OrderPaymentProcessManager.StartAsync"/> 并行发布，触发积分确认子任务。
/// </summary>
public sealed record ConfirmPointsCommand(
    Guid OrderId,
    Guid PaymentId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 订单标记已支付子任务启动命令（Process Manager → 子任务消费者）。
/// 由 <see cref="OrderPaymentProcessManager.StartAsync"/> 并行发布，触发订单状态变更（PendingPayment → Paid）。
/// 携带订单与支付单标识；子任务消费者据此加载订单与支付信息后调用 <c>Order.MarkAsPaid</c>。
/// </summary>
public sealed record MarkOrderPaidCommand(
    Guid OrderId,
    Guid PaymentId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 反向补偿命令：回滚库存确认（真实扣减 → 重新预占/释放）。
/// 由 <see cref="OrderPaymentProcessManager.HandleSubTaskFailedAsync"/> 在库存已确认时发布，
/// 供补偿消费者订阅执行反向操作。
/// </summary>
public sealed record CompensateStockConfirmCommand(
    Guid OrderId,
    Guid PaymentId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 反向补偿命令：回滚积分确认（正式扣减 → 重新冻结）。
/// 由 <see cref="OrderPaymentProcessManager.HandleSubTaskFailedAsync"/> 在积分已确认时发布。
/// </summary>
public sealed record CompensatePointsConfirmCommand(
    Guid OrderId,
    Guid PaymentId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 反向补偿命令：回滚订单标记已支付（Paid → 取消/回滚待支付）。
/// 由 <see cref="OrderPaymentProcessManager.HandleSubTaskFailedAsync"/> 在订单已标记已支付时发布。
/// </summary>
public sealed record CompensateMarkOrderPaidCommand(
    Guid OrderId,
    Guid PaymentId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}
