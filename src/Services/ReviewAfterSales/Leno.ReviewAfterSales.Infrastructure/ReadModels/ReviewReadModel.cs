namespace Leno.ReviewAfterSales.Infrastructure.ReadModels;

/// <summary>
/// 评价 ES 读模型文档，用于商品详情页评价列表与买家个人中心评价查询的 CQRS 读库。
/// 由 <see cref="ReviewReadModelSyncConsumer"/> 在评价提交/审核事件时同步索引到 Elasticsearch。
/// </summary>
public sealed class ReviewReadModel
{
    /// <summary>评价标识，作为 ES 文档 _id。</summary>
    public string ReviewId { get; set; } = string.Empty;

    /// <summary>订单标识。</summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>商品 SPU 标识。</summary>
    public string SpuId { get; set; } = string.Empty;

    /// <summary>SKU 标识。</summary>
    public string SkuId { get; set; } = string.Empty;

    /// <summary>评价人（买家）标识。</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>评分（1-5）。</summary>
    public int Rating { get; set; }

    /// <summary>评价文字内容。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>评价图片 URL 列表。</summary>
    public List<string> Images { get; set; } = new();

    /// <summary>审核状态名称（Pending/Approved/Hidden）。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>卖家回复内容，可空。</summary>
    public string? SellerReplyContent { get; set; }

    /// <summary>提交时间（UTC）。</summary>
    public DateTime SubmittedAt { get; set; }
}
