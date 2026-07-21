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
