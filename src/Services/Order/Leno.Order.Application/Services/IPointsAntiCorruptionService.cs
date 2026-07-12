namespace Leno.Order.Application.Services;

/// <summary>
/// 积分域防腐层服务接口，下单时冻结抵现积分、取消时释放冻结积分。
/// 接口定义在应用层，实现位于基础设施层，屏蔽积分域内部模型。
/// </summary>
public interface IPointsAntiCorruptionService
{
    /// <summary>
    /// 尝试计算积分可抵现金额（预览用），返回实际可抵金额。
    /// </summary>
    /// <param name="userId">买家标识。</param>
    /// <param name="pointsToUse">拟使用积分数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>实际可抵现金额。</returns>
    Task<decimal> TryOffsetAsync(Guid userId, int pointsToUse, CancellationToken ct = default);

    /// <summary>
    /// 冻结下单抵现积分。
    /// </summary>
    /// <param name="userId">买家标识。</param>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="pointsToUse">冻结积分数。</param>
    /// <param name="ct">取消令牌。</param>
    Task FreezeAsync(Guid userId, Guid orderId, int pointsToUse, CancellationToken ct = default);

    /// <summary>
    /// 释放订单冻结的积分（订单取消）。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReleaseAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 支付成功后确认扣减积分（冻结 → 正式扣减）。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task ConfirmDeductionAsync(Guid orderId, CancellationToken ct = default);
}
