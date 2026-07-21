using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Notification.Api.Controllers;

/// <summary>
/// 通知发送控制器（内部服务间调用）。
/// 受 InternalApiKeyMiddleware 保护，路由前缀为 internal/。
/// </summary>
[ApiController]
public sealed class NotificationSendController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationSendController(INotificationService notificationService)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        _notificationService = notificationService;
    }

    /// <summary>
    /// 发送通知（内部服务间调用）。
    /// </summary>
    [HttpPost("internal/v1/notifications/send")]
    [Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]
    [HttpPost("internal/notifications/send")]
    [ProducesResponseType(typeof(ApiResponse<SendNotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SendNotificationResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendAsync([FromBody] SendNotificationRequest request, CancellationToken ct)
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

        var result = await _notificationService.SendAsync(domainRequest, ct);

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