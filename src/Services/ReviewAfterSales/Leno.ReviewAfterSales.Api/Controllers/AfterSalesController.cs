using Leno.Infrastructure.Auth;
using Leno.ReviewAfterSales.Application;
using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ReviewAfterSales.Api.Controllers;

/// <summary>
/// 售后控制器。
/// 买家端：提交售后申请、撤销、查询我的售后。
/// 卖家端：查询收到的售后单。
/// 运营端：审核通过/驳回、分页查询全平台售后单。
/// </summary>
[ApiController]
public sealed class AfterSalesController : ReviewControllerBase
{
    private readonly IAfterSalesAppService _afterSalesAppService;

    public AfterSalesController(ICurrentUserContext currentUser, IAfterSalesAppService afterSalesAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(afterSalesAppService);
        _afterSalesAppService = afterSalesAppService;
    }

    // ========== 买家端 ==========

    /// <summary>买家提交售后申请。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/after-sales")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitAfterSalesAsync([FromBody] SubmitAfterSalesDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _afterSalesAppService.SubmitAfterSalesAsync(userId, dto, ct);
        return CreatedAtAction(nameof(GetAfterSalesByOrderAsync), new { orderId = result.OrderId }, ApiResponse.Success(result));
    }

    /// <summary>买家撤销售后申请。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/after-sales/{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAfterSalesAsync(Guid id, [FromBody] CancelAfterSalesDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _afterSalesAppService.CancelAfterSalesAsync(id, userId, dto.Reason, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>买家按订单查询售后单。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/after-sales/order/{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<List<AfterSalesDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAfterSalesByOrderAsync(Guid orderId, CancellationToken ct)
    {
        var result = await _afterSalesAppService.GetByOrderIdAsync(orderId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>买家我的售后单。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/after-sales/mine")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAfterSalesAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await _afterSalesAppService.GetByUserAsync(userId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    // ========== 卖家端 ==========

    /// <summary>卖家查询收到的售后单。</summary>
    [Authorize(Roles = "Seller")]
    [HttpGet("api/seller/after-sales")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSellerAfterSalesAsync(
        [FromQuery] AfterSalesStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // 当前用户为卖家，SellerId 等同于 UserId
        var sellerId = GetCurrentUserId();
        var result = await _afterSalesAppService.GetBySellerAsync(sellerId, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>卖家同意售后（转发运营审核接口，卖家即审核人）。</summary>
    [Authorize(Roles = "Seller")]
    [HttpPost("api/seller/after-sales/{id:guid}/agree")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AgreeAfterSalesAsync(Guid id, [FromBody] ApproveAfterSalesDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.ApproveAfterSalesAsync(id, operatorId, dto.ApprovedAmount, ct);
        return Ok(ApiResponse.Success());
    }

    // ========== 运营端 ==========

    /// <summary>运营审核通过售后。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/after-sales/{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveAfterSalesAsync(Guid id, [FromBody] ApproveAfterSalesDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.ApproveAfterSalesAsync(id, operatorId, dto.ApprovedAmount, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>运营驳回售后。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/after-sales/{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectAfterSalesAsync(Guid id, [FromBody] RejectAfterSalesDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        await _afterSalesAppService.RejectAfterSalesAsync(id, operatorId, dto.Reason, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>运营分页查询全平台售后单。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/after-sales")]
    [ProducesResponseType(typeof(ApiResponse<AfterSalesListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAfterSalesAsync(
        [FromQuery] Guid? orderId,
        [FromQuery] Guid? userId,
        [FromQuery] Guid? sellerId,
        [FromQuery] AfterSalesStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _afterSalesAppService.QueryAsync(orderId, userId, sellerId, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }
}
