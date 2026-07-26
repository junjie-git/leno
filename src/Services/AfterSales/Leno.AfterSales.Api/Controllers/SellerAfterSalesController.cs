using Leno.AfterSales.Application;
using Leno.AfterSales.Application.DTOs;
using Leno.AfterSales.Domain.ValueObjects;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.AfterSales.Api.Controllers;

/// <summary>
/// 卖家售后控制器（售后 BC 独立维护）。
/// 端点：查售后列表、查售后详情、同意售后、驳回售后、确认收货。
/// 全部端点需 Seller 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "Seller")]
public sealed class SellerAfterSalesController : AfterSalesControllerBase
{
    private readonly IAfterSalesAppService _afterSalesAppService;

    public SellerAfterSalesController(
        ICurrentUserContext currentUser,
        IAfterSalesAppService afterSalesAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(afterSalesAppService);
        _afterSalesAppService = afterSalesAppService;
    }

    /// <summary>卖家查询收到的售后单。</summary>
    [HttpGet("api/seller/after-sales")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSellerAfterSalesAsync(
        [FromQuery] AfterSalesStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var sellerId = GetCurrentUserId();
        var result = await _afterSalesAppService.GetBySellerAsync(sellerId, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 卖家查询售后单详情。
    /// 通过 JWT 注入 sellerId 进行归属校验，仅返回当前卖家名下售后单；
    /// 非归属卖家抛 AFTERSALES_NOT_OWNED，售后单不存在返回 404。
    /// </summary>
    [HttpGet("api/seller/after-sales/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSellerAfterSalesByIdAsync(Guid id, CancellationToken ct)
    {
        var sellerId = GetCurrentUserId();
        var result = await _afterSalesAppService.GetByIdForSellerAsync(id, sellerId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>卖家审核同意售后。</summary>
    [HttpPost("api/seller/after-sales/{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SellerApproveAfterSalesAsync(Guid id, [FromBody] ApproveAfterSalesDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.ApproveAfterSalesAsync(id, operatorId, dto.ApprovedAmount, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>卖家驳回售后。</summary>
    [HttpPost("api/seller/after-sales/{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SellerRejectAfterSalesAsync(Guid id, [FromBody] RejectAfterSalesDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.RejectAfterSalesAsync(id, operatorId, dto.Reason, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>卖家确认收到退货。</summary>
    [HttpPost("api/seller/after-sales/{id:guid}/confirm-return")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SellerConfirmReturnAsync(Guid id, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.ConfirmReturnAsync(id, operatorId, ct);
        return Ok(ApiResponse.Success());
    }
}
