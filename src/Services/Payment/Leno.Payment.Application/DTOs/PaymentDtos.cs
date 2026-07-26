using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Application.DTOs;

/// <summary>
/// 支付单查询结果 DTO。
/// </summary>
public sealed class PaymentOrderDto
{
    public Guid PaymentId { get; set; }
    public string OutTradeNo { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CNY";
    public PaymentChannel Channel { get; set; }
    public string? ChannelTradeNo { get; set; }
    public PaymentStatus Status { get; set; }
    public string? PrepayId { get; set; }
    public string? CodeUrl { get; set; }
    public string? H5Url { get; set; }
    public DateTime ExpireAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? FailReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 发起支付请求 DTO，对应 spec F-PAY-001 的 POST /api/payments 请求体。
/// 买家在前端选择支付渠道后调用本端点同步发起支付，应用服务校验订单后创建支付单并调用渠道下单，
/// 同步返回调起参数（prepayId/codeUrl/h5Url）供前端调起微信收银台或跳转支付宝。
/// </summary>
public sealed class CreatePaymentRequest
{
    /// <summary>关联订单标识，必填。应用服务经防腐层校验订单存在性、归属与可支付状态。</summary>
    public Guid OrderId { get; init; }

    /// <summary>
    /// 支付渠道，可选。未指定时由应用层按配置默认渠道选择（INV-PAY-09）。
    /// 取值范围：<see cref="PaymentChannel.WeChatPay"/> / <see cref="PaymentChannel.Alipay"/>。
    /// </summary>
    public PaymentChannel? Channel { get; init; }

    /// <summary>
    /// 支付交易类型（场景），可选。未指定时默认 <see cref="TradeType.Native"/>（扫码支付）。
    /// 微信侧映射 trade_type=JSAPI/NATIVE/H5/APP；支付宝侧映射到 precreate/wap/page/app 接口。
    /// </summary>
    public TradeType? Scene { get; init; }

    /// <summary>
    /// 幂等键，可选。优先从请求头 <c>Idempotency-Key</c> 读取；为空时由应用层按 (OrderId, Channel) 推导。
    /// 重复发起同一支付请求返回首次结果，避免重复创建支付单（INV-PAY-04）。
    /// </summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// 发起支付响应 DTO，对应 spec F-PAY-001 同步返回的调起参数。
/// 前端据 <see cref="PrepayId"/>/<see cref="CodeUrl"/>/<see cref="H5Url"/> 调起微信收银台或跳转支付宝收银台。
/// </summary>
public sealed class PaymentInitiationResultDto
{
    /// <summary>支付单标识。</summary>
    public Guid PaymentOrderId { get; init; }

    /// <summary>商户支付单号（业务可读，全局唯一），传给第三方渠道作为 out_trade_no。</summary>
    public string PaymentNo { get; init; } = string.Empty;

    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>支付渠道。</summary>
    public PaymentChannel Channel { get; init; }

    /// <summary>支付单当前状态。ChannelOrdered=渠道已下单（已拿到调起参数）；Failed=渠道下单失败。</summary>
    public PaymentStatus Status { get; init; }

    /// <summary>预支付标识（微信预支付会话标识），调起微信收银台用。</summary>
    public string? PrepayId { get; init; }

    /// <summary>扫码支付链接（微信 Native / 支付宝当面付），前端生成二维码用。</summary>
    public string? CodeUrl { get; init; }

    /// <summary>H5 支付跳转链接，前端跳转到第三方收银台用。</summary>
    public string? H5Url { get; init; }

    /// <summary>支付截止时间（UTC），超时关单。前端据本字段渲染倒计时。</summary>
    public DateTime ExpireAt { get; init; }

    /// <summary>失败原因（仅 <see cref="Status"/>=Failed 时有值），前端据本字段提示用户重试。</summary>
    public string? FailReason { get; init; }
}

/// <summary>
/// 退款单查询结果 DTO。
/// </summary>
public sealed class RefundOrderDto
{
    public Guid RefundId { get; set; }
    public string OutRefundNo { get; set; } = string.Empty;
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public Guid AfterSalesId { get; set; }
    public decimal RefundAmount { get; set; }
    public string Currency { get; set; } = "CNY";
    public PaymentChannel Channel { get; set; }
    public string? ChannelRefundNo { get; set; }
    public RefundStatus Status { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? FailReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 支付单分页查询结果。
/// </summary>
public sealed class PaymentListResultDto
{
    public List<PaymentOrderDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 退款单分页查询结果。
/// </summary>
public sealed class RefundListResultDto
{
    public List<RefundOrderDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// 主动渠道状态查询结果。
/// </summary>
public sealed class ChannelStatusDto
{
    public Guid PaymentId { get; set; }

    /// <summary>支付单所属用户标识，用于买家端接口归属校验（P0-4 IDOR 修复）。</summary>
    public Guid UserId { get; set; }

    public bool IsPaid { get; set; }
    public string? ChannelTradeNo { get; set; }
    public DateTime? PaidAt { get; set; }
}
