using Leno.Points.Application;
using Leno.Points.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Points.Api.Controllers;

/// <summary>
/// 积分域内部操作控制器，供订单域调用以试扣、冻结、释放、确认积分。
/// 路由 internal/v1/points/* 受 InternalApiKeyMiddleware 保护（X-Internal-Key 请求头）。
/// 关键：无类级 [Route]，每个 Action 显式挂 internal/v1/points/* 单路由，不再双路由叠加（旧域双路由已废弃）。
/// </summary>
[ApiController]
public sealed class InternalPointsController : ControllerBase
{
    private readonly IPointsInternalAppService _internalService;

    public InternalPointsController(IPointsInternalAppService internalService)
    {
        ArgumentNullException.ThrowIfNull(internalService);
        _internalService = internalService;
    }

    /// <summary>试算积分可抵扣金额（下单预览），不修改账户状态。</summary>
    [HttpPost("internal/v1/points/trial-offset")]
    [ProducesResponseType(typeof(ApiResponse<TrialOffsetResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TrialOffsetAsync([FromBody] TrialOffsetRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _internalService.TrialOffsetAsync(request.UserId, request.OrderAmount, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>冻结积分（下单时预占）。</summary>
    [HttpPost("internal/v1/points/freeze")]
    [ProducesResponseType(typeof(ApiResponse<FreezeResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FreezeAsync([FromBody] FreezePointsRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _internalService.FreezeAsync(request.UserId, request.Points, request.OrderId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>释放冻结积分（订单取消回退）。</summary>
    [HttpPost("internal/v1/points/release")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReleaseAsync([FromBody] ReleasePointsRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _internalService.ReleaseAsync(request.OrderId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>确认扣减冻结积分（订单支付成功后核销）。</summary>
    [HttpPost("internal/v1/points/confirm")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmAsync([FromBody] ConfirmPointsRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _internalService.ConfirmAsync(request.OrderId, ct);
        return Ok(ApiResponse.Success());
    }
}

/// <summary>试算积分抵扣请求 DTO（内部调用）。</summary>
public sealed class TrialOffsetRequestDto
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>订单金额（元）。</summary>
    public decimal OrderAmount { get; init; }
}

/// <summary>冻结积分请求 DTO（内部调用）。</summary>
public sealed class FreezePointsRequestDto
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>冻结积分数量。</summary>
    public int Points { get; init; }

    /// <summary>触发冻结的订单标识。</summary>
    public Guid OrderId { get; init; }
}

/// <summary>释放冻结积分请求 DTO（内部调用）。</summary>
public sealed class ReleasePointsRequestDto
{
    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }
}

/// <summary>确认扣减冻结积分请求 DTO（内部调用）。</summary>
public sealed class ConfirmPointsRequestDto
{
    /// <summary>关联订单标识。</summary>
    public Guid OrderId { get; init; }
}
