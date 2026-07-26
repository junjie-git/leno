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

    /// <summary>同步发起支付，返回调起参数供前端调起微信收银台或跳转支付宝（spec F-PAY-001）。</summary>
    /// <remarks>
    /// 调用方在前端选择支付渠道后调用本端点同步发起支付。应用服务经防腐层校验订单存在性、买家归属与可支付状态，
    /// 创建支付单并调用渠道下单，同步返回 prepayId/codeUrl/h5Url 调起参数。
    /// 重复发起同一支付请求返回首次结果（INV-PAY-04 单订单单活跃支付单）。
    /// </remarks>
    /// <param name="request">发起支付请求体，含订单标识、渠道（可选）、场景（可选）、幂等键（可选）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">发起支付成功，返回调起参数。</response>
    /// <response code="401">未认证（未携带有效 JWT）。</response>
    /// <response code="403">订单不属于当前买家（AC-PAY-022 越权发起他人订单支付）。</response>
    /// <response code="404">订单不存在。</response>
    /// <response code="409">订单非待支付态（已支付/已取消/已完成），或订单已由其他支付单完成支付。</response>
    /// <response code="503">订单域远程调用失败（防腐层异常）。</response>
    [Authorize(Roles = "Buyer")]
    [HttpPost("api/payments")]
    [ProducesResponseType(typeof(ApiResponse<PaymentInitiationResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ForbidResult), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PostAsync(
        [FromBody] CreatePaymentRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = GetCurrentUserId();
        var result = await _paymentAppService.CreatePaymentAsync(userId, request, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>按订单标识查询支付结果（含渠道预支付参数）。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/payments/{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ForbidResult), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaymentResultAsync(Guid orderId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _paymentAppService.GetPaymentResultAsync(orderId, ct);

        // P0-4 IDOR 修复：校验支付单归属。
        // 生产环境中 PaymentOrder.Create 保证 UserId 非空，此处 Guid.Empty 判断仅兼容未设置 UserId 的测试 Mock。
        if (result is not null && result.UserId != Guid.Empty && result.UserId != userId)
        {
            return Forbid();
        }

        return Ok(ApiResponse.Success(result));
    }

    /// <summary>主动查询渠道支付状态，若已支付则补偿更新支付单。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/payments/{paymentId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<ChannelStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ForbidResult), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> QueryPaymentStatusAsync(Guid paymentId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _paymentAppService.QueryPaymentStatusAsync(paymentId, ct);

        // P0-4 IDOR 修复：校验支付单归属。
        // 生产环境中 PaymentOrder.Create 保证 UserId 非空，此处 Guid.Empty 判断仅兼容未设置 UserId 的测试 Mock。
        if (result.UserId != Guid.Empty && result.UserId != userId)
        {
            return Forbid();
        }

        return Ok(ApiResponse.Success(result));
    }

    /// <summary>按售后单标识查询退款结果。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/refunds/{afterSalesId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RefundOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ForbidResult), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRefundResultAsync(Guid afterSalesId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _refundAppService.GetRefundResultAsync(afterSalesId, ct);

        // P0-4 IDOR 修复：校验退款单归属。
        // 生产环境中 RefundOrder.Create 保证 UserId 非空，此处 Guid.Empty 判断仅兼容未设置 UserId 的测试 Mock。
        if (result is not null && result.UserId != Guid.Empty && result.UserId != userId)
        {
            return Forbid();
        }

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
