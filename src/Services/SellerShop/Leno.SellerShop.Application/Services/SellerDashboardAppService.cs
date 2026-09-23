using Leno.SellerShop.Application.DTOs;
using Leno.SellerShop.Domain.Exceptions;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Application.Services;

/// <summary>
/// 卖家工作台应用服务实现，聚合店铺信息与 ShopMetrics 指标数据。
/// <para>双轨下线 E2（2026-09-23）：原 GetDashboardAsync（SQL 累计查询）随双轨删除，
/// 工作台概览累计指标改由 ES 读模型承载；本服务仅保留按日明细与低库存强一致查询。</para>
/// </summary>
public sealed class SellerDashboardAppService : ISellerDashboardAppService
{
    private readonly IShopRepository _shopRepository;
    private readonly IShopMetricsRepository _metricsRepository;
    private readonly IProductAntiCorruptionService _productAntiCorruptionService;

    public SellerDashboardAppService(
        IShopRepository shopRepository,
        IShopMetricsRepository metricsRepository,
        IProductAntiCorruptionService productAntiCorruptionService)
    {
        _shopRepository = shopRepository;
        _metricsRepository = metricsRepository;
        _productAntiCorruptionService = productAntiCorruptionService;
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

    /// <inheritdoc />
    public async Task<List<LowStockItemDto>> GetLowStockAlertAsync(Guid sellerId, int threshold, CancellationToken ct = default)
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

        return await _productAntiCorruptionService.GetLowStockSkusAsync(shop.Id, threshold, ct);
    }
}