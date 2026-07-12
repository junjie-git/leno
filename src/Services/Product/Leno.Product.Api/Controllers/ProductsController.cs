using Leno.Infrastructure.Auth;
using Leno.Product.Application;
using Leno.Product.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Product.Api.Controllers;

/// <summary>
/// 卖家端商品控制器，提供商品发布、编辑、上下架与查询端点。
/// 全部端点需卖家角色认证，操作校验卖家归属。
/// </summary>
[Authorize(Roles = "Seller")]
[ApiController]
[Route("api/products")]
public sealed class ProductsController : ProductControllerBase
{
    private readonly ISPUAppService _spuAppService;

    public ProductsController(ICurrentUserContext currentUser, ISPUAppService spuAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(spuAppService);
        _spuAppService = spuAppService;
    }

    /// <summary>卖家创建草稿商品。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateProductDto dto, CancellationToken ct)
    {
        var sellerId = GetCurrentUserId();
        var shopId = CurrentUser.ShopId
            ?? throw new UnauthorizedAccessException("当前卖家未关联店铺");

        var product = await _spuAppService.CreateAsync(sellerId, shopId, dto, ct);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = product.Id }, ApiResponse.Success(product));
    }

    /// <summary>卖家更新商品基础信息。</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateProductDto dto, CancellationToken ct)
    {
        var product = await _spuAppService.UpdateAsync(GetCurrentUserId(), id, dto, ct);
        return Ok(ApiResponse.Success(product));
    }

    /// <summary>卖家为商品新增 SKU。</summary>
    [HttpPost("{id:guid}/skus")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddSkuAsync(Guid id, [FromBody] AddSkuDto dto, CancellationToken ct)
    {
        var product = await _spuAppService.AddSkuAsync(GetCurrentUserId(), id, dto, ct);
        return Ok(ApiResponse.Success(product));
    }

    /// <summary>卖家提交审核。</summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitForReviewAsync(Guid id, CancellationToken ct)
    {
        await _spuAppService.SubmitForReviewAsync(GetCurrentUserId(), id, ct);
        return Ok(ApiResponse.Success("已提交审核"));
    }

    /// <summary>卖家下架商品。</summary>
    [HttpPost("{id:guid}/take-down")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TakeDownAsync(Guid id, [FromBody] ActionReasonDto dto, CancellationToken ct)
    {
        await _spuAppService.TakeDownAsync(GetCurrentUserId(), id, dto, ct);
        return Ok(ApiResponse.Success("已下架"));
    }

    /// <summary>卖家重新上架商品（进入待审核）。</summary>
    [HttpPost("{id:guid}/republish")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RepublishAsync(Guid id, CancellationToken ct)
    {
        await _spuAppService.RepublishAsync(GetCurrentUserId(), id, ct);
        return Ok(ApiResponse.Success("已重新提交审核"));
    }

    /// <summary>查询商品详情（含 SKU），买家/卖家可查。</summary>
    [Authorize]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var product = await _spuAppService.GetByIdAsync(id, ct);
        return Ok(ApiResponse.Success(product));
    }

    /// <summary>分页查询商品列表（卖家查本店，运营查全部）。</summary>
    [Authorize(Roles = "Seller,Operator,Admin")]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PageResult<ProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync([FromQuery] ProductQueryDto query, CancellationToken ct)
    {
        // 卖家仅查本店商品；运营/管理员不限店铺
        var result = await _spuAppService.QueryProductsAsync(ApplyShopScope(query), ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>卖家调整 SKU 价格。</summary>
    [HttpPost("{id:guid}/skus/{skuId:guid}/price")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdjustPriceAsync(Guid id, Guid skuId, [FromBody] AdjustPriceDto dto, CancellationToken ct)
    {
        await _spuAppService.AdjustPriceAsync(id, skuId, dto, GetCurrentUserId().ToString(), ct);
        return Ok(ApiResponse.Success("价格调整成功"));
    }

    /// <summary>查询商品价格变更历史。</summary>
    [HttpGet("{id:guid}/price-history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PriceChangeRecordDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriceHistoryAsync(Guid id, [FromQuery] Guid? skuId = null, CancellationToken ct = default)
    {
        var history = await _spuAppService.GetPriceHistoryAsync(id, skuId, ct);
        return Ok(ApiResponse.Success(history));
    }

    private ProductQueryDto ApplyShopScope(ProductQueryDto query)
    {
        if (string.Equals(CurrentUser.Role, "Seller", StringComparison.OrdinalIgnoreCase))
        {
            var shopId = CurrentUser.ShopId;
            if (shopId.HasValue)
            {
                return new ProductQueryDto
                {
                    ShopId = shopId,
                    Status = query.Status,
                    CategoryId = query.CategoryId,
                    Keyword = query.Keyword,
                    Page = query.Page,
                    PageSize = query.PageSize
                };
            }
        }

        return query;
    }
}
