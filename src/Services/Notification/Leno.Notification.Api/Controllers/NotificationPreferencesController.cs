using Leno.Infrastructure.Auth;
using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Notification.Api.Controllers;

/// <summary>
/// 用户通知偏好管理控制器。
/// </summary>
[ApiController]
public sealed class NotificationPreferencesController : NotificationControllerBase
{
    private readonly INotificationPreferenceAppService _preferenceAppService;

    public NotificationPreferencesController(ICurrentUserContext currentUser, INotificationPreferenceAppService preferenceAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(preferenceAppService);
        _preferenceAppService = preferenceAppService;
    }

    /// <summary>查询我的通知偏好。</summary>
    [Authorize(Roles = "Buyer,Seller,Operator,Admin")]
    [HttpGet("api/users/me/notification-preferences")]
    [ProducesResponseType(typeof(ApiResponse<NotificationPreferenceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPreferencesAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _preferenceAppService.GetByUserIdAsync(userId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>设置某事件渠道偏好。</summary>
    [Authorize(Roles = "Buyer,Seller,Operator,Admin")]
    [HttpPut("api/users/me/notification-preferences")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetChannelPreferenceAsync([FromBody] SetChannelPreferenceDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _preferenceAppService.SetChannelPreferenceAsync(userId, dto, ct);
        return Ok(ApiResponse.Success());
    }
}
