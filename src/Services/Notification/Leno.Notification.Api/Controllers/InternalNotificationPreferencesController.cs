using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Notification.Api.Controllers;

/// <summary>
/// 通知偏好内部端点控制器，供通知发送链路查询/更新用户偏好。
/// 路由 internal/v1/users/{userId}/notification-preferences 受 <c>InternalApiKeyMiddleware</c> 保护（X-Internal-Key 请求头）。
/// 关键：无类级 [Route]/[Authorize]，每个 Action 显式挂 internal/v1/users/{userId}/notification-preferences 单路由。
/// 对外 HTTP 端点已归 UserCenter 域（Task D1，Spec §4.3.5）。
/// </summary>
[ApiController]
public sealed class InternalNotificationPreferencesController : ControllerBase
{
    private readonly INotificationPreferenceAppService _preferenceAppService;

    public InternalNotificationPreferencesController(INotificationPreferenceAppService preferenceAppService)
    {
        ArgumentNullException.ThrowIfNull(preferenceAppService);
        _preferenceAppService = preferenceAppService;
    }

    /// <summary>查询指定用户通知偏好。</summary>
    [HttpGet("internal/v1/users/{userId:guid}/notification-preferences")]
    [ProducesResponseType(typeof(ApiResponse<NotificationPreferenceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreferencesAsync([FromRoute] Guid userId, CancellationToken ct)
    {
        var result = await _preferenceAppService.GetByUserIdAsync(userId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>设置指定用户某事件渠道偏好。</summary>
    [HttpPut("internal/v1/users/{userId:guid}/notification-preferences")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetChannelPreferenceAsync(
        [FromRoute] Guid userId,
        [FromBody] SetChannelPreferenceDto dto,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _preferenceAppService.SetChannelPreferenceAsync(userId, dto, ct);
        return Ok(ApiResponse.Success());
    }
}
