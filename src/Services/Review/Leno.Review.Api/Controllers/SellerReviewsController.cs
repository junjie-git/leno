using Leno.Infrastructure.Auth;
using Leno.Review.Application;
using Leno.Review.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Review.Api.Controllers;

/// <summary>
/// 卖家评价控制器（评价 BC 独立维护）。
/// 端点：评价列表、评价详情、回复评价。
/// 全部端点需 Seller 角色，强制按 JWT sellerId 过滤，卖家 A 无法查看/回复卖家 B 的评价（卖家隔离）。
/// </summary>
[ApiController]
[Authorize(Roles = "Seller")]
public sealed class SellerReviewsController : ReviewControllerBase
{
    private readonly IReviewAppService _reviewAppService;

    public SellerReviewsController(
        ICurrentUserContext currentUser,
        IReviewAppService reviewAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(reviewAppService);
        _reviewAppService = reviewAppService;
    }

    /// <summary>
    /// 卖家查询本店铺商品评价列表，仅返回已通过（Approved）态评价。
    /// 通过 JWT 注入 sellerId 强制过滤，卖家 A 无法查看卖家 B 的评价（卖家隔离）。
    /// 支持按评分、回复状态、商品名称（经商品域 ACL 过滤 SpuId 列表）、提交时间范围过滤。
    /// </summary>
    [HttpGet("api/seller/reviews")]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSellerReviewsAsync(
        [FromQuery] int? rating,
        [FromQuery] bool? replied,
        [FromQuery] string? productName,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var sellerId = GetCurrentUserId();
        var result = await _reviewAppService.GetBySellerAsync(
            sellerId, rating, replied, productName, startDate, endDate, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 卖家查询评价详情，校验归属卖家（sellerId 匹配）后返回单条评价。
    /// 通过 JWT sellerId 与评价聚合 SellerId 比对，防止越权查看他人店铺评价。
    /// </summary>
    [HttpGet("api/seller/reviews/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSellerReviewDetailAsync(Guid id, CancellationToken ct)
    {
        var sellerId = GetCurrentUserId();
        var result = await _reviewAppService.GetSellerReviewDetailAsync(id, sellerId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 卖家回复评价，仅已通过评价可回复，且仅归属卖家可回复。
    /// 通过 JWT sellerId 与评价聚合 SellerId 比对，防止越权回复他人店铺评价。
    /// </summary>
    [HttpPost("api/seller/reviews/{id:guid}/reply")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SellerReplyAsync(Guid id, [FromBody] SellerReplyDto dto, CancellationToken ct)
    {
        var sellerId = GetCurrentUserId();
        await _reviewAppService.SellerReplyAsync(id, sellerId, dto.Content, ct);
        return Ok(ApiResponse.Success());
    }
}
