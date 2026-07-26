using Leno.Infrastructure.Auth;
using Leno.Promotion.Application;
using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Promotion.Api.Controllers;

/// <summary>
/// 满减活动控制器（运营端），提供活动 CRUD 与启停端点。
/// 全部端点需 Operator/Admin 角色认证。
/// </summary>
[Authorize(Roles = "Operator,Admin")]
[ApiController]
[Route("api/admin/promotions")]
public sealed class PromotionsController : PromotionControllerBase
{
    private readonly IPromotionAppService _promotionAppService;

    public PromotionsController(ICurrentUserContext currentUser, IPromotionAppService promotionAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(promotionAppService);
        _promotionAppService = promotionAppService;
    }

    /// <summary>创建满减活动。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PromotionActivityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] CreatePromotionActivityDto dto, CancellationToken ct)
    {
        var activity = await _promotionAppService.CreateAsync(dto, ct);
        return Ok(ApiResponse.Success(activity));
    }

    /// <summary>更新满减活动规则。</summary>
    [HttpPut("{activityId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PromotionActivityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid activityId, [FromBody] UpdatePromotionActivityDto dto, CancellationToken ct)
    {
        var activity = await _promotionAppService.UpdateAsync(activityId, dto, ct);
        return Ok(ApiResponse.Success(activity));
    }

    /// <summary>激活满减活动。</summary>
    [HttpPost("{activityId:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateAsync(Guid activityId, CancellationToken ct)
    {
        await _promotionAppService.ActivateAsync(activityId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>暂停满减活动。</summary>
    [HttpPost("{activityId:guid}/pause")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PauseAsync(Guid activityId, CancellationToken ct)
    {
        await _promotionAppService.PauseAsync(activityId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>关闭满减活动。</summary>
    [HttpPost("{activityId:guid}/close")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseAsync(Guid activityId, CancellationToken ct)
    {
        await _promotionAppService.CloseAsync(activityId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>获取满减活动详情。</summary>
    [HttpGet("{activityId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PromotionActivityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid activityId, CancellationToken ct)
    {
        var activity = await _promotionAppService.GetByIdAsync(activityId, ct);
        return Ok(ApiResponse.Success(activity));
    }

    /// <summary>
    /// 分页查询满减活动，支持按名称模糊、状态精确、时间区间可选过滤。
    /// </summary>
    /// <param name="name">名称模糊匹配关键词。</param>
    /// <param name="status">活动状态精确匹配。</param>
    /// <param name="startTime">活动开始时间下界（>=）。</param>
    /// <param name="endTime">活动结束时间上界（<=）。</param>
    /// <param name="page">页码（从 1 开始）。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>分页结果，包含当前页活动列表与总记录数。</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PromotionListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? name,
        [FromQuery] PromotionStatus? status,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _promotionAppService.QueryAsync(name, status, startTime, endTime, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }
}
