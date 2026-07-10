using Leno.Infrastructure.Auth;
using Leno.Payment.Application;
using Leno.Payment.Application.DTOs;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Payment.Api.Controllers;

/// <summary>
/// 支付控制器。
/// 买家端（/api/payments/{orderId}）：查询订单支付结果，需 Buyer 角色。
/// 买家端（/api/payments/{paymentId}/status）：主动查询渠道支付状态，需 Buyer 角色。
/// 运营端（/api/admin/payments）：全平台支付记录分页查询，需 Operator/Admin 角色。
/// 运营端（/api/admin/refunds）：全平台退款记录分页查询，需 Operator/Admin 角色。
/// </summary>
[ApiController]
public sealed class PaymentsController : PaymentControllerBase
{
    private readonly IPaymentAppService _paymentAppService;
    private readonly IRefundAppService _refundAppService;

    public PaymentsController(
        ICurrentUserContext currentUser,
        IPaymentAppService paymentAppService,
        IRefundAppService refundAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(paymentAppService);
        ArgumentNullException.ThrowIfNull(refundAppService);
        _paymentAppService = paymentAppService;
        _refundAppService = refundAppService;
    }

    // ========== 买家端 ==========

    /// <summary>按订单标识查询支付结果（含渠道预支付参数）。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/payments/{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaymentResultAsync(Guid orderId, CancellationToken ct)
    {
        var result = await _paymentAppService.GetPaymentResultAsync(orderId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>主动查询渠道支付状态，若已支付则补偿更新支付单。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/payments/{paymentId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<ChannelStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryPaymentStatusAsync(Guid paymentId, CancellationToken ct)
    {
        var result = await _paymentAppService.QueryPaymentStatusAsync(paymentId, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>按售后单标识查询退款结果。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/refunds/{afterSalesId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RefundOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRefundResultAsync(Guid afterSalesId, CancellationToken ct)
    {
        var result = await _refundAppService.GetRefundResultAsync(afterSalesId, ct);
        return Ok(ApiResponse.Success(result));
    }

    // ========== 运营端 ==========

    /// <summary>运营端分页查询全平台支付记录。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/payments")]
    [ProducesResponseType(typeof(ApiResponse<PaymentListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryPaymentsAsync(
        [FromQuery] Guid? userId,
        [FromQuery] PaymentChannel? channel,
        [FromQuery] PaymentStatus? status,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _paymentAppService.QueryPaymentsAsync(userId, channel, status, startDate, endDate, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>运营端分页查询全平台退款记录。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/refunds")]
    [ProducesResponseType(typeof(ApiResponse<RefundListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryRefundsAsync(
        [FromQuery] Guid? orderId,
        [FromQuery] RefundStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _refundAppService.QueryRefundsAsync(orderId, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }
}
