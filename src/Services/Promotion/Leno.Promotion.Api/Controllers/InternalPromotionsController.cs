using Leno.Promotion.Application;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Promotion.Api.Controllers;

/// <summary>
/// 促销域内部接口控制器，供订单域等服务间调用。
/// 路由前缀 <c>internal/</c> 由 <c>InternalApiKeyMiddleware</c> 校验 <c>X-Internal-Key</c> 请求头。
/// </summary>
[ApiController]
public sealed class InternalPromotionsController : ControllerBase
{
    private readonly IPromotionCalculateAppService _calculateService;
    private readonly ICouponAppService _couponService;

    public InternalPromotionsController(
        IPromotionCalculateAppService calculateService,
        ICouponAppService couponService)
    {
        ArgumentNullException.ThrowIfNull(calculateService);
        ArgumentNullException.ThrowIfNull(couponService);
        _calculateService = calculateService;
        _couponService = couponService;
    }

    /// <summary>试算用户当前订单可用的优惠总金额。</summary>
    [HttpPost("internal/v1/promotions/calculate")]
    [Obsolete("双路由期保留，将于 2026-09-15 下线，请使用 internal/v1/promotions/calculate 路由", DiagnosticId = "LENO_PROMO001")]
    [HttpPost("internal/promotions/calculate")]
    [ProducesResponseType(typeof(ApiResponse<DiscountResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculateAsync([FromBody] CalculateDiscountDto input, CancellationToken ct)
    {
        var result = await _calculateService.CalculateDiscountAsync(input, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 下单锁定优惠券（Task 3），将买家持有的指定券由 Unused 置为 Locked 并绑定 orderId。
    /// 券不存在返回 404，券已被并发订单占用（非 Unused）返回业务错误码 USER_COUPON_LOCK_INVALID。
    /// </summary>
    [HttpPost("internal/v1/promotions/lock-coupon")]
    [Obsolete("双路由期保留，将于 2026-09-15 下线，请使用 internal/v1/promotions/lock-coupon 路由", DiagnosticId = "LENO_PROMO002")]
    [HttpPost("internal/promotions/lock-coupon")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LockCouponAsync([FromBody] LockCouponRequestDto input, CancellationToken ct)
    {
        await _couponService.LockCouponAsync(input.UserId, input.CouponId, input.OrderId, ct);
        return Ok(ApiResponse.Success());
    }
}
