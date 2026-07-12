using Leno.Infrastructure.Auth;
using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Notification.Api.Controllers;

/// <summary>
/// 通知频率限制管理控制器（运营端：查看/更新频率限制规则）。
/// </summary>
[ApiController]
public sealed class NotificationRateLimitsController : NotificationControllerBase
{
    private readonly IRateLimitAppService _rateLimitAppService;

    public NotificationRateLimitsController(ICurrentUserContext currentUser, IRateLimitAppService rateLimitAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(rateLimitAppService);
        _rateLimitAppService = rateLimitAppService;
    }

    /// <summary>获取指定渠道的频率限制配置。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/notification-rate-limits")]
    [ProducesResponseType(typeof(ApiResponse<RateLimitConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRateLimitAsync([FromQuery] NotificationChannel channel, CancellationToken ct)
    {
        var result = await _rateLimitAppService.GetRateLimitAsync(channel, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>更新指定渠道的频率限制配置。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPut("api/admin/notification-rate-limits")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRateLimitAsync(
        [FromQuery] NotificationChannel channel,
        [FromBody] SaveRateLimitConfigDto dto,
        CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _rateLimitAppService.UpdateRateLimitAsync(operatorId, channel, dto, ct);
        return Ok(ApiResponse.Success());
    }
}