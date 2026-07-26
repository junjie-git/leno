using Leno.Infrastructure.Auth;
using Leno.Points.Application;
using Leno.Points.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Points.Api.Controllers;

/// <summary>
/// 积分控制器（买家端）。
/// 路由 /api/points/*，需 Buyer 角色。
/// 对应 design-prompts operations/08-membership-ops/points.md 的 4 个买家端端点：
/// 每日签到、积分账户查询、积分流水查询、积分兑换优惠券。
/// </summary>
[ApiController]
[Route("api/points")]
[Authorize(Roles = "Buyer")]
public sealed class PointsController : ControllerBase
{
    private readonly IPointsAppService _pointsAppService;
    private readonly ICheckInAppService _checkInAppService;
    private readonly IExchangeCouponAppService _exchangeCouponAppService;
    private readonly ICurrentUserContext _currentUser;

    public PointsController(
        IPointsAppService pointsAppService,
        ICheckInAppService checkInAppService,
        IExchangeCouponAppService exchangeCouponAppService,
        ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(pointsAppService);
        ArgumentNullException.ThrowIfNull(checkInAppService);
        ArgumentNullException.ThrowIfNull(exchangeCouponAppService);
        ArgumentNullException.ThrowIfNull(currentUser);
        _pointsAppService = pointsAppService;
        _checkInAppService = checkInAppService;
        _exchangeCouponAppService = exchangeCouponAppService;
        _currentUser = currentUser;
    }

    /// <summary>每日签到，计算连续签到奖励并发放积分。</summary>
    [HttpPost("check-in")]
    [ProducesResponseType(typeof(ApiResponse<CheckInResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckInAsync(CancellationToken ct)
    {
        var result = await _checkInAppService.CheckInAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>查询当前用户积分账户余额与累计统计。</summary>
    [HttpGet("account")]
    [ProducesResponseType(typeof(ApiResponse<PointsAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccountAsync(CancellationToken ct)
    {
        var result = await _pointsAppService.GetAccountAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>分页查询当前用户积分流水。</summary>
    [HttpGet("ledger")]
    [ProducesResponseType(typeof(ApiResponse<List<PointsFlowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLedgerAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _pointsAppService.GetLedgerAsync(GetCurrentUserId(), page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>积分兑换优惠券，扣减积分并发布兑换请求事件。</summary>
    [HttpPost("exchange-coupon")]
    [ProducesResponseType(typeof(ApiResponse<ExchangeCouponResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExchangeCouponAsync([FromBody] ExchangeCouponRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _exchangeCouponAppService.ExchangeAsync(
            GetCurrentUserId(), request.CouponTemplateId, request.PointsRequired, ct);
        return Ok(ApiResponse.Success(result));
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("未认证");
        }

        return _currentUser.UserId.Value;
    }
}

/// <summary>
/// 积分兑换优惠券请求 DTO（API 层）。
/// UserId 从 JWT 解析，不由客户端传入。
/// </summary>
public sealed class ExchangeCouponRequestDto
{
    /// <summary>优惠券模板标识。</summary>
    public Guid CouponTemplateId { get; init; }

    /// <summary>本次兑换需要的积分数量。</summary>
    public int PointsRequired { get; init; }
}
