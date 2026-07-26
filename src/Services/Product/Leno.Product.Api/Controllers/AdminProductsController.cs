using Leno.Infrastructure.Auth;
using Leno.Product.Application;
using Leno.Product.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Product.Api.Controllers;

/// <summary>
/// 运营端商品审核控制器，提供商品审核通过/驳回、库存补货与全量商品列表端点。
/// 仅 Admin/Operator 角色可访问。
/// </summary>
[Authorize(Roles = "Admin,Operator")]
[ApiController]
[Route("api/admin/products")]
public sealed class AdminProductsController : ProductControllerBase
{
    private readonly ISPUAppService _spuAppService;
    private readonly IInventoryAppService _inventoryAppService;

    public AdminProductsController(
        ICurrentUserContext currentUser,
        ISPUAppService spuAppService,
        IInventoryAppService inventoryAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(spuAppService);
        ArgumentNullException.ThrowIfNull(inventoryAppService);
        _spuAppService = spuAppService;
        _inventoryAppService = inventoryAppService;
    }

    /// <summary>运营审核通过上架。</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken ct)
    {
        await _spuAppService.ApproveAsync(id, GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success("已审核通过并上架"));
    }

    /// <summary>运营审核驳回。</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectAsync(Guid id, [FromBody] ActionReasonDto dto, CancellationToken ct)
    {
        await _spuAppService.RejectAsync(id, GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success("已驳回"));
    }

    /// <summary>批量审核通过上架。单个失败不阻塞整批，结果返回成功与失败明细。</summary>
    [HttpPost("batch-approve")]
    [ProducesResponseType(typeof(ApiResponse<BatchOperationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BatchApproveAsync([FromBody] BatchReviewRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reviewedBy = GetCurrentUserId();
        var result = await _spuAppService.BatchApproveAsync(request.Ids, reviewedBy, request.Reason, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>批量审核驳回。单个失败不阻塞整批，结果返回成功与失败明细。</summary>
    [HttpPost("batch-reject")]
    [ProducesResponseType(typeof(ApiResponse<BatchOperationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BatchRejectAsync([FromBody] BatchReviewRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Ok(ApiResponse.Fail<BatchOperationResultDto>(400, "驳回原因不可为空"));
        }
        var reviewedBy = GetCurrentUserId();
        var result = await _spuAppService.BatchRejectAsync(request.Ids, reviewedBy, request.Reason!, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>卖家/运营为指定 SKU 补货。</summary>
    [HttpPost("skus/{skuId:guid}/replenish")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplenishAsync(Guid skuId, [FromBody] ReplenishStockDto dto, CancellationToken ct)
    {
        await _inventoryAppService.ReplenishAsync(skuId, dto, ct);
        return Ok(ApiResponse.Success("补货成功"));
    }

    /// <summary>运营/管理员调整商品 SKU 库存（delta 方式）。</summary>
    [HttpPost("{id:guid}/skus/{skuId:guid}/stock")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStockAsync(Guid id, Guid skuId, [FromBody] UpdateStockDto dto, CancellationToken ct)
    {
        await _spuAppService.UpdateStockAsync(id, skuId, dto, GetCurrentUserId().ToString(), ct);
        return Ok(ApiResponse.Success("库存调整成功"));
    }

    /// <summary>运营/管理员全量商品列表（跨店铺查询，支持按卖家、状态、分类、关键词过滤）。</summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(ApiResponse<PageResult<ProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllProductsAsync([FromQuery] ProductQueryDto query, CancellationToken ct)
    {
        // 运营/管理员不限店铺，不强制限制 ShopId
        var result = await _spuAppService.QueryProductsAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }
}
