using Leno.Infrastructure.Auth;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.PointsMembership.Api.Controllers;

/// <summary>
/// 积分控制器。
/// 买家端（/api/points）：每日签到、积分账户查询、积分流水查询、积分兑换优惠券，需 Buyer 角色。
/// 运营端（/api/admin/points）：手动发放积分，需 Operator/Admin 角色。
/// </summary>
[ApiController]
public sealed class PointsController : PointsMembershipControllerBase
{
    private readonly IPointsAppService _pointsAppService;
    private readonly IExchangeCouponAppService _exchangeCouponAppService;

    public PointsController(
        ICurrentUserContext currentUser,
        IPointsAppService pointsAppService,
        IExchangeCouponAppService exchangeCouponAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(pointsAppService);
        ArgumentNullException.ThrowIfNull(exchangeCouponAppService);
        _pointsAppService = pointsAppService;
        _exchangeCouponAppService = exchangeCouponAppService;
    }

    // ========== 买家端 ==========

    /// <summary>每日签到，计算连续签到奖励并发放积分。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/points/check-in")]
    [ProducesResponseType(typeof(ApiResponse<CheckInResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckInAsync(CancellationToken ct)
    {
        var result = await _pointsAppService.CheckInAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>查询当前用户积分账户余额与累计统计。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/points/account")]
    [ProducesResponseType(typeof(ApiResponse<PointsAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccountAsync(CancellationToken ct)
    {
        var account = await _pointsAppService.GetPointsAccountAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(account));
    }

    /// <summary>分页查询当前用户积分流水。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/points/ledger")]
    [ProducesResponseType(typeof(ApiResponse<List<PointsLedgerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLedgerAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var ledger = await _pointsAppService.GetLedgerAsync(GetCurrentUserId(), page, pageSize, ct);
        return Ok(ApiResponse.Success(ledger));
    }

    /// <summary>积分兑换优惠券，冻结积分并发布兑换请求事件。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/points/exchange-coupon")]
    [ProducesResponseType(typeof(ApiResponse<ExchangeCouponResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExchangeCouponAsync([FromBody] ExchangeCouponDto input, CancellationToken ct)
    {
        var result = await _exchangeCouponAppService.ExchangeCouponAsync(input, ct);
        return Ok(ApiResponse.Success(result));
    }

    // ========== 运营端 ==========

    /// <summary>运营手动发放积分。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/points/award")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AwardAsync([FromBody] AwardPointsDto dto, CancellationToken ct)
    {
        await _pointsAppService.AwardPointsAsync(dto, ct);
        return Ok(ApiResponse.Success());
    }
}
