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
    /// <summary>被评价商品归属卖家标识，由订单域防腐层查询填充，用于卖家侧评价列表归属校验与卖家隔离验证。</summary>
    public Guid SellerId { get; set; }
    public int Rating { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> Images { get; set; } = [];
    public ReviewStatus Status { get; set; }
    public string? SellerReplyContent { get; set; }
    /// <summary>卖家回复操作人标识，回复后填充，用于审计。</summary>
    public Guid? SellerReplyBy { get; set; }
    /// <summary>卖家回复时间（UTC），回复后填充。</summary>
    public DateTime? SellerReplyAt { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? AuditedAt { get; set; }
    public DateTime? HiddenAt { get; set; }

    /// <summary>追评内容，已通过评价可追评一次，可空表示未追评。</summary>
    public string? AppendContent { get; set; }

    /// <summary>追评图片 URL 列表，未追评时为空列表。</summary>
    public List<string> AppendImages { get; set; } = [];

    /// <summary>追评时间（UTC），追评后填充。</summary>
    public DateTime? AppendedAt { get; set; }
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

/// <summary>
/// 买家追评请求 DTO。
/// </summary>
public sealed class AppendReviewDto
{
    /// <summary>追评内容，1-500 字。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>追评图片 URL 列表，最多 9 张。</summary>
    public List<string> Images { get; set; } = [];
}
