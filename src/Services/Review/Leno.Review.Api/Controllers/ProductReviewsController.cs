using Leno.Review.Application;
using Leno.Review.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Review.Api.Controllers;

/// <summary>
/// 商品评价控制器（评价 BC 独立维护）。
/// 端点：按 SPU 分页查询已通过评价（商品详情页）。
/// 匿名可查，无需鉴权。
/// </summary>
[ApiController]
public sealed class ProductReviewsController : ControllerBase
{
    private readonly IReviewAppService _reviewAppService;

    public ProductReviewsController(IReviewAppService reviewAppService)
    {
        ArgumentNullException.ThrowIfNull(reviewAppService);
        _reviewAppService = reviewAppService;
    }

    /// <summary>按 SPU 分页查询已通过评价（商品详情页）。</summary>
    [HttpGet("api/products/{spuId:guid}/reviews")]
    [ProducesResponseType(typeof(ApiResponse<ReviewListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviewsBySpuAsync(Guid spuId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _reviewAppService.GetReviewsBySpuAsync(spuId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }
}
