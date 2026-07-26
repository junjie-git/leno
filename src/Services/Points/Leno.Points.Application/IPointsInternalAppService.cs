using Leno.Points.Application.DTOs;

namespace Leno.Points.Application;

/// <summary>
/// 积分域内部应用服务接口，供其他 BC（如订单域、会员域）经 internal HTTP 端点或 gRPC 调用。
/// 契约与旧域 PointsMembership.IPointsInternalAppService 业务行为对齐，确保调用方零改造。
/// </summary>
public interface IPointsInternalAppService
{
    /// <summary>
    /// 试算积分可抵扣金额（下单预览），不修改账户状态。
    /// 按 100 积分 = 1 元换算，根据用户可用余额与订单金额计算最优抵扣。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="orderAmount">订单金额（元）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>试算结果，包含可抵扣金额与使用的积分数量。</returns>
    Task<TrialOffsetResultDto> TrialOffsetAsync(Guid userId, decimal orderAmount, CancellationToken ct = default);

    /// <summary>
    /// 冻结积分（下单时预占），校验余额充足，扣减可用余额、累加冻结余额。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="points">冻结积分数量，须 &gt; 0。</param>
    /// <param name="orderId">触发冻结的订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>冻结结果，包含成功状态与冻结后余额。</returns>
    Task<FreezeResultDto> FreezeAsync(Guid userId, int points, Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 释放冻结积分（订单取消回退），按订单反查冻结记录，回退至可用余额。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReleaseAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 确认扣减冻结积分（订单支付成功后核销），按订单反查冻结记录，扣减冻结余额、累加累计消耗。
    /// 与 <see cref="ReleaseAsync"/> 共用 GetByFrozenOrderIdAsync 反查路径。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task ConfirmAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 等级提升奖励积分入账（消费 MemberLevelChangedIntegrationEvent 后调用）。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="amount">奖励积分数量，须 &gt; 0。</param>
    /// <param name="newLevel">触发奖励的会员等级编号。</param>
    /// <param name="ct">取消令牌。</param>
    Task GrantLevelBonusAsync(Guid userId, int amount, int newLevel, CancellationToken ct = default);
}
