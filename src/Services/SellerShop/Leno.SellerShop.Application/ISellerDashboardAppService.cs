using Leno.SellerShop.Application.DTOs;

namespace Leno.SellerShop.Application;

/// <summary>
/// 卖家工作台应用服务，提供工作台概览、销售趋势与店铺运营指标查询。
/// 指标数据由订单域、商品域、评价域事件驱动的 ShopMetrics 聚合维护。
/// </summary>
public interface ISellerDashboardAppService
{
    /// <summary>查询卖家工作台概览（店铺信息 + 当日运营指标）。</summary>
    [Obsolete("请使用 IQueryHandler<ShopDashboardQuery, ShopDashboardResult> 读 ES 读模型，将在 2026-10-01 移除。" +
              "迁移步骤：(1) P0-2/P0-3 修复后 ES 读模型数据完整；(2) 开启 Dashboard:EnableComparison 双发对比验证数据一致性；" +
              "(3) 切换 Dashboard:UseReadModel=true 灰度到 ES；(4) 观察 1 周无差异后移除本方法。" +
              "调用方：SellerDashboardController.GetDashboardAsync（Feature Flag 关闭时调用）。")]
    Task<SellerDashboardDto> GetDashboardAsync(Guid sellerId, CancellationToken ct = default);

    /// <summary>查询指定店铺与日期范围的销售趋势（按日序列，用于图表）。</summary>
    Task<IReadOnlyList<SalesTrendDto>> GetSalesTrendAsync(
        Guid shopId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    /// <summary>查询指定店铺与日期范围的运营指标明细。</summary>
    Task<IReadOnlyList<ShopMetricsDto>> GetShopMetricsAsync(
        Guid shopId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
