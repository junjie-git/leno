using Leno.Order.Application.ProcessManagers.States;

namespace Leno.Order.Application.ProcessManagers;

/// <summary>
/// 订单支付流程编排状态仓储接口，管理 <see cref="OrderPaymentProcessState"/> 持久化。
/// 接口定义在 Application 层，由 Infrastructure 层（<c>EfCoreOrderPaymentProcessRepository</c>）实现。
/// 三个子任务可能并发回写状态，实现需依赖 EF Core 乐观锁（<see cref="OrderPaymentProcessState.RowVersion"/>）检测冲突。
/// </summary>
public interface IOrderPaymentProcessRepository
{
    /// <summary>
    /// 保存（新增或更新）流程状态。
    /// 新增时 <see cref="OrderPaymentProcessState.ProcessId"/> 为新建 Guid；
    /// 更新时由 EF Core 检查 <see cref="OrderPaymentProcessState.RowVersion"/> 乐观锁，冲突抛 <c>DbUpdateConcurrencyException</c>。
    /// </summary>
    /// <param name="state">流程状态实例。</param>
    /// <param name="ct">取消令牌。</param>
    Task SaveAsync(OrderPaymentProcessState state, CancellationToken ct = default);

    /// <summary>
    /// 按订单标识查询进行中的支付流程状态。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流程状态实例，不存在返回 null。</returns>
    Task<OrderPaymentProcessState?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 按流程标识查询支付流程状态。
    /// </summary>
    /// <param name="processId">流程实例标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>流程状态实例，不存在返回 null。</returns>
    Task<OrderPaymentProcessState?> GetByProcessIdAsync(Guid processId, CancellationToken ct = default);
}
