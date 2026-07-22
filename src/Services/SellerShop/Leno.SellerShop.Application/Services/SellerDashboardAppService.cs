using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Application.Services;

/// <summary>
/// 卖家工作台应用服务实现，聚合店铺信息、ShopMetrics 指标数据与 ShopDashboardData 经营数据。
/// </summary>
public sealed class SellerDashboardAppService : ISellerDashboardAppService
{
    private readonly IShopRepository _shopRepository;
    private readonly IShopMetricsRepository _metricsRepository;
    private readonly IShopDashboardRepository _dashboardRepository;

    public SellerDashboardAppService(
        IShopRepository shopRepository,
        IShopMetricsRepository metricsRepository,
        IShopDashboardRepository dashboardRepository)
    {
        _shopRepository = shopRepository;
        _metricsRepository = metricsRepository;
        _dashboardRepository = dashboardRepository;
    }

    /// <inheritdoc />
    [Obsolete("请使用 IQueryHandler<ShopDashboardQuery, ShopDashboardResult> 读 ES 读模型，将在 2026-10-01 移除。" +
              "迁移步骤：(1) P0-2/P0-3 修复后 ES 读模型数据完整；(2) 开启 Dashboard:EnableComparison 双发对比验证数据一致性；" +
              "(3) 切换 Dashboard:UseReadModel=true 灰度到 ES；(4) 观察 1 周无差异后移除本方法。" +
              "调用方：SellerDashboardController.GetDashboardAsync（Feature Flag 关闭时调用）。")]
    public async Task<SellerDashboardDto> GetDashboardAsync(Guid sellerId, CancellationToken ct = default)
    {
        if (sellerId == Guid.Empty)
        {
            throw new SellerShopDomainException("卖家账号标识不可为空", "SELLER_USER_EMPTY");
        }

        var shop = await _shopRepository.GetBySellerIdAsync(sellerId, ct);
        if (shop is null)
        {
            throw new SellerShopDomainException("店铺不存在", "SHOP_NOT_FOUND");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var metrics = await _metricsRepository.GetByShopIdAsync(shop.Id, today, ct);
        var dashboard = await _dashboardRepository.GetByShopIdAsync(shop.Id, ct);

        return new SellerDashboardDto
        {
            ShopId = shop.Id,
            ShopName = shop.ShopName,
            Status = shop.Status,
            ProductCount = shop.ProductCount,
            TotalOrders = dashboard?.TotalOrders ?? 0,
            PendingOrders = dashboard?.PendingOrders ?? 0,
            CompletedOrders = dashboard?.CompletedOrders ?? 0,
            TotalRevenue = dashboard?.TotalRevenue ?? 0m,
            TodayOrderCount = metrics?.OrderCount ?? 0,
            TodaySalesAmount = metrics?.SalesAmount.Amount ?? 0m,
            TodaySalesCurrency = metrics?.SalesAmount.Currency ?? "CNY",
            TodayAvgRating = metrics?.AvgRating ?? 0m,
            TodayRatingCount = metrics?.RatingCount ?? 0,
            TodayRefundCount = metrics?.RefundCount ?? 0
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SalesTrendDto>> GetSalesTrendAsync(
        Guid shopId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        EnsureValidRange(fromDate, toDate);

        var metrics = await _metricsRepository.GetByDateRangeAsync(shopId, fromDate, toDate, ct);

        return metrics
            .Select(m => new SalesTrendDto
            {
                Date = m.Date,
                OrderCount = m.OrderCount,
                SalesAmount = m.SalesAmount.Amount,
                SalesCurrency = m.SalesAmount.Currency,
                AvgRating = m.AvgRating
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShopMetricsDto>> GetShopMetricsAsync(
        Guid shopId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        EnsureValidRange(fromDate, toDate);

        var metrics = await _metricsRepository.GetByDateRangeAsync(shopId, fromDate, toDate, ct);

        return metrics
            .Select(m => new ShopMetricsDto
            {
                ShopId = m.ShopId,
                Date = m.Date,
                OrderCount = m.OrderCount,
                SalesAmount = m.SalesAmount.Amount,
                SalesCurrency = m.SalesAmount.Currency,
                ProductCount = m.ProductCount,
                AvgRating = m.AvgRating,
                RatingCount = m.RatingCount,
                RefundCount = m.RefundCount
            })
            .ToList();
    }

    private static void EnsureValidRange(DateOnly fromDate, DateOnly toDate)
    {
        if (fromDate > toDate)
        {
            throw new SellerShopDomainException("起始日期不可晚于结束日期", "METRICS_INVALID_RANGE");
        }
    }
}