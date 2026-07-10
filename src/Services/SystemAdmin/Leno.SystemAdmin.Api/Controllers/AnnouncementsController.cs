using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 系统公告管理控制器（运营端 CRUD、发布/撤回与公开已发布查询）。
/// </summary>
[ApiController]
public sealed class AnnouncementsController : SystemAdminControllerBase
{
    private readonly IAnnouncementAppService _announcementAppService;

    public AnnouncementsController(ICurrentUserContext currentUser, IAnnouncementAppService announcementAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(announcementAppService);
        _announcementAppService = announcementAppService;
    }

    /// <summary>分页查询公告，支持类型与状态过滤。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/announcements")]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] AnnouncementType? type,
        [FromQuery] AnnouncementStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _announcementAppService.QueryAsync(type, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>创建公告（初始为草稿态）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/announcements")]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveAnnouncementDto dto, CancellationToken ct)
    {
        var result = await _announcementAppService.CreateAsync(dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>更新公告（仅草稿态可更新）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPut("api/admin/announcements/{announcementId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid announcementId, [FromBody] SaveAnnouncementDto dto, CancellationToken ct)
    {
        var result = await _announcementAppService.UpdateAsync(announcementId, dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>发布公告并发布集成事件。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/announcements/{announcementId:guid}/publish")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishAsync(Guid announcementId, CancellationToken ct)
    {
        await _announcementAppService.PublishAsync(announcementId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>撤回公告（仅已发布态可撤回）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/announcements/{announcementId:guid}/unpublish")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnpublishAsync(Guid announcementId, CancellationToken ct)
    {
        await _announcementAppService.UnpublishAsync(announcementId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>分页查询当前有效（已发布且未过期）的公告，公开查询。</summary>
    [Authorize(Roles = "Buyer,Seller,Operator,Admin")]
    [HttpGet("api/announcements")]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublishedAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _announcementAppService.GetPublishedAsync(page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }
}
