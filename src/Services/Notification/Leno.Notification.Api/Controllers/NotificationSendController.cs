using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Api.Controllers;

/// <summary>
/// 通知发送控制器（内部服务间调用）。
/// 受 InternalApiKeyMiddleware 保护，路由前缀为 internal/。
/// </summary>
[ApiController]
public sealed class NotificationSendController : ControllerBase
{
    /// <summary>旧路由模板，下线倒计时阶段保留，调用时打弃用告警日志便于监控迁移进度。</summary>
    private const string LegacyRoute = "internal/notifications/send";

    /// <summary>新路由模板，下线旧路由后唯一保留的入口。</summary>
    private const string CurrentRoute = "internal/v1/notifications/send";

    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationSendController> _logger;

    public NotificationSendController(
        INotificationService notificationService,
        ILogger<NotificationSendController> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(logger);
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// 发送通知（内部服务间调用，当前路由）。
    /// </summary>
    [HttpPost(CurrentRoute)]
    [ProducesResponseType(typeof(ApiResponse<SendNotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SendNotificationResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendAsync([FromBody] SendNotificationRequest request, CancellationToken ct)
    {
        return await ExecuteSendAsync(request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 发送通知（旧路由，双路由期保留，1 周后下线，请使用 <see cref="SendAsync"/>）。
    /// </summary>
    [Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/notifications/send 路由")]
    [HttpPost(LegacyRoute)]
    [ProducesResponseType(typeof(ApiResponse<SendNotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SendNotificationResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendLegacyAsync([FromBody] SendNotificationRequest request, CancellationToken ct)
    {
        // P2-47：旧路由被调用时记录告警日志，便于监控迁移进度、触发告警，下线时间到达后此方法将整体删除。
        var caller = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        var templateCode = request?.TemplateCode ?? "<null>";
        _logger.LogWarning(
            "已弃用路由被调用 Route={Route} Caller={Caller} TemplateCode={TemplateCode} IdempotencyKey={IdempotencyKey}；请迁移至 {CurrentRoute}，1 周后旧路由将被移除",
            LegacyRoute,
            caller,
            templateCode,
            request?.IdempotencyKey ?? "<null>",
            CurrentRoute);

        return await ExecuteSendAsync(request!, ct).ConfigureAwait(false);
    }

    private async Task<IActionResult> ExecuteSendAsync(SendNotificationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateCode))
        {
            return BadRequest(ApiResponse.Fail<SendNotificationResponse>(400, "模板编码不可为空"));
        }

        if (request.UserId == Guid.Empty)
        {
            return BadRequest(ApiResponse.Fail<SendNotificationResponse>(400, "用户标识不可为空"));
        }

        var domainRequest = new NotificationRequest
        {
            TemplateCode = request.TemplateCode,
            UserId = request.UserId,
            Variables = request.Variables ?? [],
            IdempotencyKey = request.IdempotencyKey ?? string.Empty,
            BusinessRef = request.BusinessRef ?? string.Empty
        };

        var result = await _notificationService.SendAsync(domainRequest, ct).ConfigureAwait(false);

        var response = new SendNotificationResponse
        {
            Succeeded = result.Succeeded,
            RecordId = result.RecordId,
            ErrorCode = result.ErrorCode,
            ErrorMessage = result.ErrorMessage
        };

        if (result.Succeeded)
        {
            return Ok(ApiResponse.Success(response));
        }

        // P2-41：发送失败时返回 HTTP 400 BadRequest，而非 200 OK 携带 body code=400，使调用方按状态码正确处理失败。
        return BadRequest(ApiResponse.Fail<SendNotificationResponse>(400, result.ErrorMessage ?? "发送失败", response));
    }
}
