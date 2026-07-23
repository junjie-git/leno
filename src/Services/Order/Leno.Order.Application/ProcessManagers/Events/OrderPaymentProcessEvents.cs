using MassTransit;

namespace Leno.Order.Application.ProcessManagers.Events;

/// <summary>
/// 订单支付流程编排事件与子任务命令契约。
/// 这些消息由 <see cref="OrderPaymentProcessManager"/> 发布，供监控、后续全量切流消费者订阅。
/// 双轨期（<see cref="OrderPaymentProcessOptions.UsePaymentProcessManager"/>=true）下，
/// 子任务命令作为编排信号发布；旧路径（直接消费 PaymentSucceededEvent 的消费者）仍执行实际工作，
/// Process Manager 跟踪三个子任务完成度并在全部完成后发布 <see cref="OrderPaymentProcessCompleted"/>。
/// </summary>

/// <summary>
/// 支付流程编排已启动事件（Process Manager 内部）。
/// 由 <see cref="OrderPaymentProcessManager.StartAsync"/> 在创建状态记录后发布，
/// 通知下游三个子任务（MarkOrderPaid / ConfirmStock / ConfirmPoints）开始并行执行。
/// </summary>
public sealed record OrderPaymentProcessStarted(
    Guid OrderId,
    Guid PaymentId,
    Guid ProcessId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 库存确认子任务完成事件（Process Manager 内部）。
/// 由 <see cref="Leno.Order.Infrastructure.Consumers.StockConfirmConsumer"/> 在完成库存确认后
/// 调用 <see cref="OrderPaymentProcessManager.HandleStockConfirmedAsync"/> 时触发，
/// 标记 <see cref="States.OrderPaymentProcessState.StockConfirmed"/>=true。
/// </summary>
public sealed record StockConfirmed(
    Guid OrderId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 积分确认子任务完成事件（Process Manager 内部）。
/// 由 <see cref="Leno.Order.Infrastructure.Consumers.PointsConfirmConsumer"/> 在完成积分确认后
/// 调用 <see cref="OrderPaymentProcessManager.HandlePointsConfirmedAsync"/> 时触发，
/// 标记 <see cref="States.OrderPaymentProcessState.PointsConfirmed"/>=true。
/// </summary>
public sealed record PointsConfirmed(
    Guid OrderId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 订单标记已支付子任务完成事件（Process Manager 内部）。
/// 由 <see cref="Leno.Order.Infrastructure.Consumers.PaymentSucceededEventConsumer"/> 在完成 MarkAsPaid 后
/// 调用 <see cref="OrderPaymentProcessManager.HandleOrderMarkedPaidAsync"/> 时触发，
/// 标记 <see cref="States.OrderPaymentProcessState.OrderMarkedPaid"/>=true。
/// </summary>
public sealed record OrderMarkedPaid(
    Guid OrderId) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 支付流程整体完成事件（Process Manager 内部）。
/// 由 <see cref="OrderPaymentProcessManager.TryCompleteAsync"/> 在三个子任务全部完成时发布，
/// 通知下游（发货、通知等后续业务）支付后编排已结束。
/// </summary>
public sealed record OrderPaymentProcessCompleted(
    Guid OrderId,
    Guid PaymentId,
    Guid ProcessId,
    DateTime CompletedAt) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 支付流程进入补偿中事件（Process Manager 内部）。
/// 由 <see cref="OrderPaymentProcessManager.HandleSubTaskFailedAsync"/> 在任一子任务失败时发布，
/// 标识流程进入 <c>Compensating</c> 状态，对已完成子任务发布反向补偿命令。
/// </summary>
public sealed record OrderPaymentProcessCompensating(
    Guid OrderId,
    Guid ProcessId,
    string FailedSubTask,
    string Reason) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}

/// <summary>
/// 子任务失败事件（Process Manager 内部）。
/// 携带失败的子任务名称与失败原因，由 <see cref="OrderPaymentProcessManager.HandleSubTaskFailedAsync"/> 消费，
/// 触发对已完成子任务的反向补偿。
/// </summary>
public sealed record SubTaskFailed(
    Guid OrderId,
    string SubTask,
    string Reason) : CorrelatedBy<Guid>
{
    /// <summary>关联标识，与 <see cref="OrderId"/> 一致。</summary>
    public Guid CorrelationId => OrderId;
}
