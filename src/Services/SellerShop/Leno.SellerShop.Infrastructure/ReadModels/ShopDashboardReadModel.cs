namespace Leno.SellerShop.Infrastructure.ReadModels;

/// <summary>
/// 店铺工作台 ES 读模型文档，索引名 <see cref="ShopDashboardIndexName"/>。
/// 用于卖家工作台与运营分析的快速检索，聚合店铺基础信息、订单统计与评论统计。
/// 由 3 个集成事件驱动重建（IndexAsync 覆盖更新）：
/// <list type="bullet">
/// <item><see cref="OrderCreatedShopDashboardSyncConsumer"/>（订单创建，<c>OrderCreatedEvent.SellerId</c> 即 ShopId）</item>
/// <item><see cref="OrderCompletedShopDashboardSyncConsumer"/>（订单完成，<c>OrderCompletedEvent.SellerId</c> 即 ShopId）</item>
/// <item><see cref="ReviewSubmittedShopDashboardSyncConsumer"/>（评价提交，<c>ReviewSubmittedEvent.SpuId</c> 经映射得 ShopId）</item>
/// </list>
/// 字段值由 <see cref="IShopDashboardReadModelBuilder"/> 从 SellerShop BC 既有聚合（Shop、ShopDashboardData）读取后重建。
/// </summary>
public sealed class ShopDashboardReadModel
{
    /// <summary>店铺工作台读模型索引名。</summary>
    public const string ShopDashboardIndexName = "leno_shop_dashboards";

    /// <summary>店铺标识，作为 ES 文档 _id。</summary>
    public Guid ShopId { get; init; }

    /// <summary>店铺名称。</summary>
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

    /// <summary>累计已退款金额（已支付订单取消时累加），用于计算 <see cref="NetSales"/>。</summary>
    public decimal RefundedAmount { get; init; }

    /// <summary>净销售收入 = <see cref="TotalSales"/> - <see cref="RefundedAmount"/>，供工作台展示真实经营收入。</summary>
    public decimal NetSales { get; init; }

    /// <summary>币种（ISO 4217）。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>经营数据最近一次更新时间（UTC，来自领域聚合 LastUpdatedAt）。</summary>
    public DateTime LastUpdatedAt { get; init; }

    /// <summary>读模型索引时间（UTC）。</summary>
    public DateTime IndexedAt { get; init; }

    /// <summary>
    /// 读模型模式版本号，用于后续字段演进时消费方按版本路由反序列化逻辑。
    /// </summary>
    public int SchemaVersion { get; init; } = 1;
}
