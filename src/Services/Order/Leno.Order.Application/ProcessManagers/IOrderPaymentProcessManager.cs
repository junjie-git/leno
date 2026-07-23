using Leno.Order.Application.ProcessManagers.States;

namespace Leno.Order.Application.ProcessManagers;

/// <summary>
/// 订单支付流程编排器接口，定义 Process Manager 的编排契约。
/// 实现类 <see cref="OrderPaymentProcessManager"/> 编排支付成功后三个并行子任务
/// （<c>MarkOrderPaid</c> / <c>ConfirmStock</c> / <c>ConfirmPoints</c>），跟踪整体完成状态。
/// 消费者通过本接口将子任务完成回调转发给 Process Manager（双轨期 feature flag 控制）。
/// </summary>
public interface IOrderPaymentProcessManager
{
    /// <summary>
    /// 启动支付流程编排：创建状态记录，并行发布三个子任务命令
    /// （<see cref="Commands.ConfirmStockCommand"/> / <see cref="Commands.ConfirmPointsCommand"/> / <see cref="Commands.MarkOrderPaidCommand"/>）。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="paymentId">支付单标识。</param>
    /// <param name="paymentChannel">支付渠道（用于 MarkOrderPaidCommand）。</param>
    /// <param name="tradeNo">第三方交易号。</param>
    /// <param name="amount">实付金额。</param>
    /// <param name="currency">币种。</param>
    /// <param name="paidAt">支付时间（UTC）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>创建的流程状态实例。</returns>
    Task<OrderPaymentProcessState> StartAsync(
        Guid orderId,
        Guid paymentId,
        string paymentChannel,
        string tradeNo,
        decimal amount,
        string currency,
        DateTime paidAt,
        CancellationToken ct = default);

    /// <summary>
    /// 处理库存确认子任务完成回调：加载状态，设置 <see cref="OrderPaymentProcessState.StockConfirmed"/>=true，调用 <see cref="TryCompleteAsync"/>。
    /// 幂等：若已确认则不重复发布命令。
    /// </summary>
    Task HandleStockConfirmedAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 处理积分确认子任务完成回调：加载状态，设置 <see cref="OrderPaymentProcessState.PointsConfirmed"/>=true，调用 <see cref="TryCompleteAsync"/>。
    /// 幂等：若已确认则不重复发布命令。
    /// </summary>
    Task HandlePointsConfirmedAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 处理订单标记已支付子任务完成回调：加载状态，设置 <see cref="OrderPaymentProcessState.OrderMarkedPaid"/>=true，调用 <see cref="TryCompleteAsync"/>。
    /// 幂等：若已标记则不重复发布命令。
    /// </summary>
    Task HandleOrderMarkedPaidAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 尝试完成流程：若三个标志都为 true，设 <see cref="OrderPaymentProcessState.CurrentState"/>=Completed，
    /// 发布 <see cref="Events.OrderPaymentProcessCompleted"/> 事件；否则仅按完成度更新中间状态并保存。
    /// </summary>
    Task TryCompleteAsync(OrderPaymentProcessState state, CancellationToken ct = default);

    /// <summary>
    /// 处理子任务失败：设 <see cref="OrderPaymentProcessState.CurrentState"/>=Compensating，
    /// 对已完成的子任务发布反向补偿命令
    /// （<see cref="Commands.CompensateStockConfirmCommand"/> / <see cref="Commands.CompensatePointsConfirmCommand"/> / <see cref="Commands.CompensateMarkOrderPaidCommand"/>）。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="subTask">失败的子任务名称（Stock / Points / MarkOrderPaid）。</param>
    /// <param name="ct">取消令牌。</param>
    Task HandleSubTaskFailedAsync(Guid orderId, string subTask, CancellationToken ct = default);
}
