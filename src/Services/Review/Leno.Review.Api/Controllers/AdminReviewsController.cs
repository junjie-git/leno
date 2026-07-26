using Leno.Infrastructure.Auth;
using Leno.Review.Application;
using Leno.Review.Application.DTOs;
using Leno.Review.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Review.Api.Controllers;

/// <summary>
/// 运营评价控制器（评价 BC 独立维护）。
/// 端点：分页查询评价、审核通过评价、隐藏违规评价。
/// 全部端点需 Operator 或 Admin 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminReviewsController : ReviewControllerBase
{
    private readonly IReviewAppService _reviewAppService;

    public AdminReviewsController(
        ICurrentUserContext currentUser,
        IReviewAppService reviewAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(reviewAppService);
        _reviewAppService = reviewAppService;
    }

    /// <summary>运营分页查询评价（按状态过滤）。</summary>
    [HttpGet("api/admin/reviews")]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryReviewsAsync(
        [FromQuery] ReviewStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _reviewAppService.QueryReviewsAsync(status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>运营审核通过评价，将待审核态置为已通过态。</summary>
    [HttpPost("api/admin/reviews/{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveReviewAsync(Guid id, CancellationToken ct)
    {
        var auditorId = GetCurrentUserId();
        await _reviewAppService.ApproveReviewAsync(id, auditorId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>运营隐藏违规评价，将已通过态置为已隐藏态。</summary>
    [HttpPost("api/admin/reviews/{id:guid}/hide")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> HideReviewAsync(Guid id, [FromBody] ModerateReviewDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _reviewAppService.HideReviewAsync(id, operatorId, dto.Reason ?? string.Empty, ct);
        return Ok(ApiResponse.Success());
    }
}
