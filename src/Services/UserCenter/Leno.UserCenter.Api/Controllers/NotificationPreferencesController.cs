using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.UserCenter.Application;
using Leno.UserCenter.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserCenter.Api.Controllers;

/// <summary>
/// 通知偏好控制器，提供查询与更新用户通知偏好端点。
/// 全部端点需 Buyer 角色认证；首次查询时由应用层懒初始化默认偏好并持久化。
/// 从 UserAuth BC 迁入 UserCenter BC（Task A6）。
/// 端点契约对齐 docs/design-prompts/buyer-app/12-notification/preferences.md
/// 与 docs/design-prompts/buyer-app/13-profile/settings.md（消息推送开关联动）。
/// </summary>
[Authorize(Roles = "Buyer")]
[ApiController]
[Route("api/users/me/notification-preferences")]
public sealed class NotificationPreferencesController : UserCenterControllerBase
{
    private readonly INotificationPreferencesAppService _appService;

    public NotificationPreferencesController(
        ICurrentUserContext currentUser,
        INotificationPreferencesAppService appService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(appService);
        _appService = appService;
    }

    /// <summary>查询当前用户通知偏好。首次访问时懒初始化为默认偏好并持久化。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<NotificationPreferencesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var result = await _appService.GetAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 更新通知偏好。支持两种模式：
    /// 1) 单事件单渠道：body 含 eventType/channel/enabled 三字段；
    /// 2) 批量矩阵：body.batchSettings 非空时全量替换偏好矩阵。
    /// 免打扰字段（dndEnabled/dndStart/dndEnd）独立处理，两种模式均可单独更新。
    /// 站内信渠道始终强制开启，不可关闭。
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<NotificationPreferencesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateNotificationPreferencesRequest request, CancellationToken ct)
    {
        var result = await _appService.UpdateAsync(GetCurrentUserId(), request, ct);
        return Ok(ApiResponse.Success(result));
    }
}
