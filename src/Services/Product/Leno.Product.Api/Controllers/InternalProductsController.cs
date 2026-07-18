using Leno.Product.Application;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Product.Api.Controllers;

/// <summary>
/// 商品域内部查询控制器，供其他微服务调用。
/// 受 InternalApiKeyMiddleware 保护（X-Internal-Key 头部鉴权），不经过 JWT 鉴权。
/// </summary>
[ApiController]
public sealed class InternalProductsController : ControllerBase
{
    private readonly IProductInternalQueryService _queryService;

    public InternalProductsController(IProductInternalQueryService queryService)
    {
        ArgumentNullException.ThrowIfNull(queryService);
        _queryService = queryService;
    }

    /// <summary>按 SKU 标识查询其概要信息。</summary>
    [HttpGet("internal/v1/products/skus/{skuId:guid}")]
    [Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]
    [HttpGet("internal/products/skus/{skuId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SkuInfoResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSkuInfoAsync(Guid skuId, CancellationToken ct)
    {
        var result = await _queryService.GetSkuInfoAsync(skuId, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "SKU 不存在"));
        }

        return Ok(ApiResponse.Success(result));
    }

    /// <summary>批量查询 SKU 概要信息，跳过不存在的 SKU。</summary>
    [HttpPost("internal/v1/products/skus/batch")]
    [Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]
    [HttpPost("internal/products/skus/batch")]
    [ProducesResponseType(typeof(ApiResponse<List<SkuInfoResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSkuInfosBatchAsync([FromBody] List<Guid> skuIds, CancellationToken ct)
    {
        var results = await _queryService.GetSkuInfosBatchAsync(skuIds, ct);
        return Ok(ApiResponse.Success(results));
    }
}
