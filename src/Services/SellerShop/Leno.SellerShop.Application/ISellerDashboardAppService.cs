using Leno.SellerShop.Application.DTOs;

namespace Leno.SellerShop.Application;

/// <summary>
/// 卖家工作台应用服务，提供销售趋势与店铺运营指标查询。
/// 指标数据由订单域、商品域、评价域事件驱动的 ShopMetrics 聚合维护。
/// <para>双轨下线 E2（2026-09-23）：工作台概览累计指标改由 ES 读模型承载
/// （<c>IQueryHandler&lt;ShopDashboardQuery, ShopDashboardResult&gt;</c>），
/// 本服务仅保留按日明细与低库存等强一致 SQL 查询。</para>
/// </summary>
public interface ISellerDashboardAppService
{
    /// <summary>查询指定店铺与日期范围的销售趋势（按日序列，用于图表）。</summary>
    Task<IReadOnlyList<SalesTrendDto>> GetSalesTrendAsync(
        Guid shopId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    /// <summary>查询指定店铺与日期范围的运营指标明细。</summary>
    Task<IReadOnlyList<ShopMetricsDto>> GetShopMetricsAsync(
        Guid shopId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    /// <summary>
    /// 查询当前卖家店铺的低库存 SKU 列表（经 ACL 调商品域）。
    /// </summary>
    /// <param name="sellerId">卖家标识（取自 JWT）。</param>
    /// <param name="threshold">低库存阈值。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>低库存 SKU 列表。</returns>
    Task<List<LowStockItemDto>> GetLowStockAlertAsync(Guid sellerId, int threshold, CancellationToken ct = default);
}
