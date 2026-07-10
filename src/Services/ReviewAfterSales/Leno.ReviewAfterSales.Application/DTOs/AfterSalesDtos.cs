using Leno.ReviewAfterSales.Domain.ValueObjects;

namespace Leno.ReviewAfterSales.Application.DTOs;

/// <summary>
/// 提交售后申请请求 DTO。
/// </summary>
public sealed class SubmitAfterSalesDto
{
    public Guid OrderId { get; set; }
    public Guid? OrderLineId { get; set; }
    public Guid SellerId { get; set; }
    public AfterSalesType Type { get; set; }
    public string ReasonCategory { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<string> Images { get; set; } = [];
    public decimal RequestedAmount { get; set; }
    public string Currency { get; set; } = "CNY";
}

/// <summary>
/// 审核同意售后请求 DTO。
/// </summary>
public sealed class ApproveAfterSalesDto
{
    public decimal ApprovedAmount { get; set; }
}

/// <summary>
/// 审核驳回售后请求 DTO。
/// </summary>
public sealed class RejectAfterSalesDto
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 买家撤销售后请求 DTO。
/// </summary>
public sealed class CancelAfterSalesDto
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 售后单查询结果 DTO。
/// </summary>
public sealed class AfterSalesDto
{
    public Guid AfterSalesId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? OrderLineId { get; set; }
    public Guid UserId { get; set; }
    public Guid SellerId { get; set; }
    public AfterSalesType Type { get; set; }
    public string ReasonCategory { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<string> Images { get; set; } = [];
    public decimal RequestedAmount { get; set; }
    public string Currency { get; set; } = "CNY";
    public decimal? ApprovedAmount { get; set; }
    public decimal? RefundedAmount { get; set; }
    public AfterSalesStatus Status { get; set; }
    public DateTime AppliedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApproverId { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? ChannelRefundNo { get; set; }
    public string? RejectReason { get; set; }
    public string? FailReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
}

/// <summary>
/// 售后单分页查询结果。
/// </summary>
public sealed class AfterSalesListResultDto
{
    public List<AfterSalesDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
