using Leno.PointsMembership.Application;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.PointsMembership.Api.Controllers;

/// <summary>
/// 积分域内部操作控制器，供订单域调用以试扣、冻结、释放积分。
/// 路由前缀 internal/ 受 InternalApiKeyMiddleware 保护（X-Internal-Key 请求头）。
/// </summary>
[ApiController]
public sealed class InternalPointsController : ControllerBase
{
    private readonly IPointsInternalAppService _service;

    public InternalPointsController(IPointsInternalAppService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <summary>试算积分可抵扣金额，不修改账户状态。</summary>
    [HttpPost("internal/v1/points/trial-offset")]
    [Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]
    [HttpPost("internal/points/trial-offset")]
    [ProducesResponseType(typeof(ApiResponse<TrialOffsetResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TrialOffsetAsync([FromBody] TrialOffsetDto input, CancellationToken ct)
    {
        var result = await _service.TrialOffsetAsync(input, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>冻结积分（下单预占）。</summary>
    [HttpPost("internal/v1/points/freeze")]
    [Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]
    [HttpPost("internal/points/freeze")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> FreezeAsync([FromBody] FreezePointsDto input, CancellationToken ct)
    {
        await _service.FreezeAsync(input, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>释放冻结积分（订单取消回退）。</summary>
    [HttpPost("internal/v1/points/release")]
    [Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]
    [HttpPost("internal/points/release")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReleaseAsync([FromBody] ReleasePointsDto input, CancellationToken ct)
    {
        await _service.ReleaseAsync(input, ct);
        return Ok(ApiResponse.Success());
    }
}
