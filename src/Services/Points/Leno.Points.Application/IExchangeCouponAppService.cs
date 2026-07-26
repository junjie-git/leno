using Leno.Points.Application.DTOs;

namespace Leno.Points.Application;

/// <summary>
/// 积分兑换优惠券应用服务接口，编排冻结积分、创建兑换聚合用例。
/// </summary>
public interface IExchangeCouponAppService
{
    /// <summary>
    /// 积分兑换优惠券：扣减积分、创建兑换聚合、发布兑换请求事件给优惠券域。
    /// 余额不足时抛出 <c>PointsDomainException</c>（错误码 POINTS_BALANCE_INSUFFICIENT）。
    /// </summary>
    /// <param name="userId">发起兑换的用户标识。</param>
    /// <param name="couponTemplateId">优惠券模板标识。</param>
    /// <param name="pointsRequired">本次兑换需要的积分数量。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>兑换结果，包含兑换记录标识与冻结积分数量。</returns>
    Task<ExchangeCouponResultDto> ExchangeAsync(Guid userId, Guid couponTemplateId, int pointsRequired, CancellationToken ct = default);
}
