using Leno.ReviewAfterSales.Domain.ValueObjects;

namespace Leno.ReviewAfterSales.Application.DTOs;

/// <summary>
/// 提交评价请求 DTO。
/// </summary>
public sealed class SubmitReviewDto
{
    public Guid OrderId { get; set; }
    public Guid OrderLineId { get; set; }
    public Guid SpuId { get; set; }
    public Guid SkuId { get; set; }
    public int Rating { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> Images { get; set; } = [];
}

/// <summary>
/// 卖家回复评价请求 DTO。
/// </summary>
public sealed class SellerReplyDto
{
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 运营审核评价请求 DTO（隐藏时附带原因）。
/// </summary>
public sealed class ModerateReviewDto
{
    public string? Reason { get; set; }
}

/// <summary>
/// 评价查询结果 DTO。
/// </summary>
public sealed class ReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderLineId { get; set; }
    public Guid SpuId { get; set; }
    public Guid SkuId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> Images { get; set; } = [];
    public ReviewStatus Status { get; set; }
    public string? SellerReplyContent { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? AuditedAt { get; set; }
    public DateTime? HiddenAt { get; set; }
}

/// <summary>
/// 评价分页查询结果。
/// </summary>
public sealed class ReviewListResultDto
{
    public List<ReviewDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
