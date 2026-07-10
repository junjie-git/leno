using Leno.Infrastructure.Auth;
using Leno.ReviewAfterSales.Application;
using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ReviewAfterSales.Api.Controllers;

/// <summary>
/// 评价控制器。
/// 买家端：提交评价、查询商品评价、查询订单行评价、我的评价。
/// 卖家端：回复评价。
/// 运营端：审核通过/隐藏评价、分页查询。
/// </summary>
[ApiController]
public sealed class ReviewsController : ReviewControllerBase
{
    private readonly IReviewAppService _reviewAppService;

    public ReviewsController(ICurrentUserContext currentUser, IReviewAppService reviewAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(reviewAppService);
        _reviewAppService = reviewAppService;
    }

    // ========== 买家端 ==========

    /// <summary>买家提交评价。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/reviews")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitReviewAsync([FromBody] SubmitReviewDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _reviewAppService.SubmitReviewAsync(userId, dto, ct);
        return CreatedAtAction(nameof(GetReviewByOrderLineAsync), new { orderLineId = result.OrderLineId }, ApiResponse.Success(result));
    }

    /// <summary>按订单行查询评价。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/reviews/order-line/{orderLineId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviewByOrderLineAsync(Guid orderLineId, CancellationToken ct)
    {
        var result = await _reviewAppService.GetReviewByOrderLineAsync(orderLineId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>按 SPU 分页查询已通过评价（商品详情页）。</summary>
    [HttpGet("api/products/{spuId:guid}/reviews")]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviewsBySpuAsync(Guid spuId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _reviewAppService.GetReviewsBySpuAsync(spuId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>买家我的评价。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/reviews/mine")]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReviewsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _reviewAppService.GetReviewsByUserAsync(userId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    // ========== 卖家端 ==========

    /// <summary>卖家回复评价。</summary>
    [Authorize(Roles = "Seller")]
    [HttpPost("api/reviews/{id:guid}/reply")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SellerReplyAsync(Guid id, [FromBody] SellerReplyDto dto, CancellationToken ct)
    {
        await _reviewAppService.SellerReplyAsync(id, dto.Content, ct);
        return Ok(ApiResponse.Success());
    }

    // ========== 运营端 ==========

    /// <summary>运营审核通过评价。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/reviews/{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveReviewAsync(Guid id, CancellationToken ct)
    {
        var auditorId = GetCurrentUserId();
        await _reviewAppService.ApproveReviewAsync(id, auditorId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>运营隐藏违规评价。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/reviews/{id:guid}/hide")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> HideReviewAsync(Guid id, [FromBody] ModerateReviewDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _reviewAppService.HideReviewAsync(id, operatorId, dto.Reason ?? string.Empty, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>运营分页查询评价。</summary>
    [Authorize(Roles = "Operator,Admin")]
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
}
