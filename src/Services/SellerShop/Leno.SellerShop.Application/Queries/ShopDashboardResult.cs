namespace Leno.SellerShop.Application.Queries;

/// <summary>
/// 卖家工作台概览查询结果（CQRS 读侧 Query Result）。
/// 字段映射自 <c>ShopDashboardReadModel</c>：店铺基础信息、订单状态分布、评论统计与销售收入。
/// </summary>
public sealed class ShopDashboardResult
{
    public Guid ShopId { get; init; }

    public string ShopName { get; init; } = string.Empty;

    /// <summary>累计订单总数（含已取消）。</summary>
    public int TotalOrders { get; init; }

    /// <summary>待处理订单数（待发货/待支付）。</summary>
    public int PendingOrders { get; init; }

    /// <summary>已确认订单数（已支付待发货）。</summary>
    public int ConfirmedOrders { get; init; }

    /// <summary>已完成订单数。</summary>
    public int CompletedOrders { get; init; }

    /// <summary>已取消订单数。</summary>
    public int CancelledOrders { get; init; }

    /// <summary>累计评价总数。</summary>
    public int TotalReviews { get; init; }

    /// <summary>平均评分（1-5，保留两位小数）。</summary>
    public decimal AverageRating { get; init; }

    /// <summary>五星评价数。</summary>
    public int FiveStarReviews { get; init; }

    /// <summary>一星评价数。</summary>
    public int OneStarReviews { get; init; }

    /// <summary>累计销售收入。</summary>
    public decimal TotalSales { get; init; }

    /// <summary>累计已退款金额（已支付订单取消时累加）。</summary>
    public decimal RefundedAmount { get; init; }

    /// <summary>净销售收入 = <see cref="TotalSales"/> - <see cref="RefundedAmount"/>，供工作台展示真实经营收入。</summary>
    public decimal NetSales { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>经营数据最近一次更新时间（UTC，来自领域聚合 LastUpdatedAt）。</summary>
    public DateTime? LastUpdatedAt { get; init; }

    /// <summary>读模型索引时间（UTC），用于排查同步延迟。</summary>
    public DateTime? IndexedAt { get; init; }
}
