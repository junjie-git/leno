using Leno.Infrastructure.Auth;
using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Notification.Api.Controllers;

/// <summary>
/// 通知控制器（买家端站内信查询与已读管理）。
/// </summary>
[ApiController]
public sealed class NotificationsController : NotificationControllerBase
{
    private readonly INotificationAppService _notificationAppService;

    public NotificationsController(ICurrentUserContext currentUser, INotificationAppService notificationAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(notificationAppService);
        _notificationAppService = notificationAppService;
    }

    /// <summary>分页查询我的站内信。</summary>
    [Authorize(Roles = "Buyer,Seller,Operator,Admin")]
    [HttpGet("api/notifications")]
    [ProducesResponseType(typeof(ApiResponse<NotificationListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationsAsync(
        [FromQuery] bool? isRead,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationAppService.GetNotificationsAsync(userId, isRead, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>获取未读计数。</summary>
    [Authorize(Roles = "Buyer,Seller,Operator,Admin")]
    [HttpGet("api/notifications/unread-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCountAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var count = await _notificationAppService.GetUnreadCountAsync(userId, ct);
        return Ok(ApiResponse.Success(count));
    }

    /// <summary>按记录标识批量标记已读。</summary>
    [Authorize(Roles = "Buyer,Seller,Operator,Admin")]
    [HttpPost("api/notifications/read")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsReadAsync([FromBody] MarkAsReadDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _notificationAppService.MarkAsReadAsync(userId, dto.RecordIds, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>全部标记已读。</summary>
    [Authorize(Roles = "Buyer,Seller,Operator,Admin")]
    [HttpPost("api/notifications/read-all")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsReadAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _notificationAppService.MarkAllAsReadAsync(userId, ct);
        return Ok(ApiResponse.Success());
    }
}
