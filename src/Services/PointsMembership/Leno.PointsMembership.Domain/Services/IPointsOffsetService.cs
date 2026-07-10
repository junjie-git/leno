namespace Leno.PointsMembership.Domain.Services;

/// <summary>
/// 积分抵扣防腐层接口，供订单域调用以试算、冻结、确认扣减与释放积分。
/// 实现位于应用/基础设施层，订单域不直接依赖积分域领域模型，避免上下文耦合。
/// </summary>
public interface IPointsOffsetService
{
    /// <summary>
    /// 试算积分可抵扣金额（如 100 积分 = 1 元），不修改账户状态。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="pointsToUse">拟使用积分数量，须 &gt; 0。</param>
    /// <returns>可抵扣金额（元）。</returns>
    Task<decimal> TryOffsetAsync(Guid userId, int pointsToUse, CancellationToken ct = default);

    /// <summary>
    /// 冻结积分（下单预占），校验余额充足。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="orderId">触发冻结的订单标识。</param>
    /// <param name="pointsToUse">冻结积分数量，须 &gt; 0。</param>
    Task FreezeAsync(Guid userId, Guid orderId, int pointsToUse, CancellationToken ct = default);

    /// <summary>
    /// 确认扣减积分（订单支付成功核销冻结）。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    Task ConfirmDeductAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 释放冻结积分（订单取消回退）。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    Task ReleaseAsync(Guid orderId, CancellationToken ct = default);
}
