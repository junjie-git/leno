using Leno.Inventory.Domain.Repositories;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Inventory.Api.Controllers;

/// <summary>
/// 库存域内部查询控制器，供其他微服务调用。
/// 受 InternalApiKeyMiddleware 保护（internal/ 前缀路由）。
/// 暴露按 SKU 查询可用库存的内部 API。
/// </summary>
[ApiController]
public sealed class InternalInventoryController : ControllerBase
{
    private readonly IInventoryRepository _inventoryRepository;

    public InternalInventoryController(IInventoryRepository inventoryRepository)
    {
        ArgumentNullException.ThrowIfNull(inventoryRepository);
        _inventoryRepository = inventoryRepository;
    }

    /// <summary>
    /// 查询指定 SKU 的当前可用库存（Redis 权威值）。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>可用库存数量。</returns>
    [HttpGet("internal/v1/inventory/stock/{skuId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StockAvailableDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableAsync(Guid skuId, CancellationToken ct)
    {
        var available = await _inventoryRepository.GetAvailableAsync(skuId, ct);
        return Ok(ApiResponse.Success(new StockAvailableDto(skuId, available)));
    }
}

/// <summary>
/// 库存可用量查询结果 DTO。
/// </summary>
public sealed record StockAvailableDto(Guid SkuId, int AvailableQty);
