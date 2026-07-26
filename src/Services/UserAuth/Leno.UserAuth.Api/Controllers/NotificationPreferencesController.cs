using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// 通知偏好控制器，提供查询与更新通知偏好端点。
/// 全部端点需 Buyer 角色认证。
/// 端点契约对齐 docs/design-prompts/buyer-app/12-notification/preferences.md 与 13-profile/settings.md。
/// </summary>
[Authorize(Roles = "Buyer")]
[ApiController]
[Route("api/users/me/notification-preferences")]
public sealed class NotificationPreferencesController : UserAuthControllerBase
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

    /// <summary>查询当前用户通知偏好。首次访问懒初始化为默认偏好。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<NotificationPreferencesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var result = await _appService.GetAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 更新当前用户通知偏好。支持单事件单渠道与批量矩阵两种模式。
    /// 站内信渠道始终强制开启；免打扰字段独立处理。
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<NotificationPreferencesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken ct)
    {
        var result = await _appService.UpdateAsync(GetCurrentUserId(), request, ct);
        return Ok(ApiResponse.Success(result));
    }
}
