using System.Text.Json;

namespace Leno.ApiGateway.Bff.Models;

/// <summary>
/// 商家工作台聚合响应：店铺看板 + 最近订单 + 评论统计。
/// </summary>
public sealed class SellerDashboardBffResponse
{
    /// <summary>店铺看板（来自 SellerShop BC <c>/api/seller/{sellerId}/dashboard</c>）。</summary>
    public JsonElement? ShopDashboard { get; init; }

    /// <summary>最近订单列表（来自 Order BC <c>/api/orders?sellerId={sellerId}&amp;pageIndex=0&amp;pageSize=5</c>）。</summary>
    public JsonElement? RecentOrders { get; init; }

    /// <summary>评论统计（来自 ReviewAfterSales BC <c>/api/reviews?sellerId={sellerId}</c>）。</summary>
    public JsonElement? ReviewStats { get; init; }
}
