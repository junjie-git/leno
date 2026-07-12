using Leno.PointsMembership.Application.DTOs;

namespace Leno.PointsMembership.Application;

/// <summary>
/// 积分兑换优惠券应用服务，编排冻结积分、发布兑换请求事件。
/// </summary>
public interface IExchangeCouponAppService
{
    /// <summary>
    /// 积分兑换优惠券：冻结积分，发布 PointsExchangeCouponRequestedEvent 给优惠券域。
    /// </summary>
    /// <param name="input">兑换参数。</param>
    Task<ExchangeCouponResultDto> ExchangeCouponAsync(ExchangeCouponDto input, CancellationToken ct = default);
}