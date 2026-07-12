using Leno.Infrastructure.Auth;
using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Notification.Api.Controllers;

/// <summary>
/// 通知渠道配置管理控制器（运营端：查看/更新配置、测试发送）。
/// </summary>
[ApiController]
public sealed class NotificationConfigController : NotificationControllerBase
{
    private readonly INotificationConfigAppService _configAppService;

    public NotificationConfigController(ICurrentUserContext currentUser, INotificationConfigAppService configAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(configAppService);
        _configAppService = configAppService;
    }

    /// <summary>获取指定渠道的配置（敏感字段脱敏显示）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/notification-config")]
    [ProducesResponseType(typeof(ApiResponse<NotificationConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfigAsync([FromQuery] NotificationChannel channel, CancellationToken ct)
    {
        var result = await _configAppService.GetConfigAsync(channel, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>更新指定渠道的配置。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPut("api/admin/notification-config")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateConfigAsync(
        [FromQuery] NotificationChannel channel,
        [FromBody] SaveNotificationConfigDto dto,
        CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _configAppService.UpdateConfigAsync(operatorId, channel, dto, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>测试发送验证渠道配置是否正确。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/notification-config/test")]
    [ProducesResponseType(typeof(ApiResponse<TestSendResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestSendAsync(
        [FromQuery] NotificationChannel channel,
        [FromBody] TestSendRequestDto dto,
        CancellationToken ct)
    {
        var result = await _configAppService.TestSendAsync(channel, dto, ct);
        return Ok(ApiResponse.Success(result));
    }
}