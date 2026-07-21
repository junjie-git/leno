using Leno.Infrastructure.Auth;
using Leno.Notification.Application;
using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Notification.Api.Controllers;

/// <summary>
/// 通知模板管理控制器（运营端 CRUD + 预览）。
/// </summary>
[ApiController]
public sealed class NotificationTemplatesController : NotificationControllerBase
{
    private readonly INotificationTemplateAppService _templateAppService;

    public NotificationTemplatesController(ICurrentUserContext currentUser, INotificationTemplateAppService templateAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(templateAppService);
        _templateAppService = templateAppService;
    }

    /// <summary>创建通知模板。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/notification-templates")]
    [ProducesResponseType(typeof(ApiResponse<NotificationTemplateDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveNotificationTemplateDto dto, CancellationToken ct)
    {
        var result = await _templateAppService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetByIdAsync), new { templateId = result.TemplateId }, ApiResponse.Success(result));
    }

    /// <summary>按标识查询模板。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/notification-templates/{templateId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<NotificationTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid templateId, CancellationToken ct)
    {
        // 走主键查询，避免全表加载后内存 FirstOrDefault
        var item = await _templateAppService.GetByIdAsync(templateId, ct);
        return Ok(ApiResponse.Success(item));
    }

    /// <summary>更新通知模板。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPut("api/admin/notification-templates/{templateId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<NotificationTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid templateId, [FromBody] SaveNotificationTemplateDto dto, CancellationToken ct)
    {
        var result = await _templateAppService.UpdateAsync(templateId, dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>启用模板。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/notification-templates/{templateId:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(Guid templateId, CancellationToken ct)
    {
        await _templateAppService.EnableAsync(templateId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>禁用模板。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/notification-templates/{templateId:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(Guid templateId, CancellationToken ct)
    {
        await _templateAppService.DisableAsync(templateId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>分页查询模板列表。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/notification-templates")]
    [ProducesResponseType(typeof(ApiResponse<NotificationTemplateListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] string? eventType,
        [FromQuery] NotificationChannel? channel,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _templateAppService.QueryTemplatesAsync(eventType, channel, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>预览模板渲染结果。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/notification-templates/{templateId:guid}/preview")]
    [ProducesResponseType(typeof(ApiResponse<TemplatePreviewResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewAsync(Guid templateId, [FromBody] PreviewTemplateDto dto, CancellationToken ct)
    {
        var result = await _templateAppService.PreviewAsync(templateId, dto, ct);
        return Ok(ApiResponse.Success(result));
    }
}
