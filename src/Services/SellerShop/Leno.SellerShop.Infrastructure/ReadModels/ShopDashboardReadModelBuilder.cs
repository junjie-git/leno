using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.ReadModels;

/// <summary>
/// <see cref="IShopDashboardReadModelBuilder"/> 默认实现。
/// 注入 SellerShop BC 既有仓储（<see cref="IShopRepository"/>、<see cref="IShopDashboardRepository"/>）
/// 与评论域防腐层（<see cref="IReviewAntiCorruptionService"/>）查询最新聚合根与评论统计，
/// 投影为 <see cref="ShopDashboardReadModel"/>；店铺不存在时返回 null。
/// 评论统计 fail-closed：防腐层返回 null 时按零值兜底并记 Warning。
/// </summary>
public sealed class ShopDashboardReadModelBuilder : IShopDashboardReadModelBuilder
{
    private readonly IShopRepository _shopRepository;
    private readonly IShopDashboardRepository _dashboardRepository;
    private readonly IReviewAntiCorruptionService _reviewAntiCorruption;
    private readonly ILogger<ShopDashboardReadModelBuilder> _logger;

    public ShopDashboardReadModelBuilder(
        IShopRepository shopRepository,
        IShopDashboardRepository dashboardRepository,
        IReviewAntiCorruptionService reviewAntiCorruption,
        ILogger<ShopDashboardReadModelBuilder> logger)
    {
        _shopRepository = shopRepository ?? throw new ArgumentNullException(nameof(shopRepository));
        _dashboardRepository = dashboardRepository ?? throw new ArgumentNullException(nameof(dashboardRepository));
        _reviewAntiCorruption = reviewAntiCorruption ?? throw new ArgumentNullException(nameof(reviewAntiCorruption));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ShopDashboardReadModel?> BuildAsync(Guid shopId, CancellationToken ct)
    {
        if (shopId == Guid.Empty)
        {
            _logger.LogWarning("构建店铺工作台读模型失败：ShopId 为空");
            return null;
        }

        var shop = await _shopRepository.GetByIdAsync(shopId, ct);
        if (shop is null)
        {
            _logger.LogWarning("店铺 {ShopId} 不存在，跳过工作台读模型构建", shopId);
            return null;
        }

        // 经营数据可能尚未建立（无任何订单事件到达），按零值兜底
        var dashboard = await _dashboardRepository.GetByShopIdAsync(shopId, ct);

        // 通过防腐层反查评论统计；fail-closed 返回 null 时按零值兜底
        var reviewStats = await _reviewAntiCorruption.GetReviewStatisticsAsync(shopId, ct).ConfigureAwait(false);
        if (reviewStats is null)
        {
            _logger.LogWarning("评论域防腐层返回 null，ShopId={ShopId} 评论统计按零值兜底", shopId);
        }

        var now = DateTime.UtcNow;
        var readModel = new ShopDashboardReadModel
        {
            ShopId = shop.Id,
            ShopName = shop.ShopName,
            TotalOrders = dashboard?.TotalOrders ?? 0,
            PendingOrders = dashboard?.PendingOrders ?? 0,
            ConfirmedOrders = dashboard?.ConfirmedOrders ?? 0,
            CompletedOrders = dashboard?.CompletedOrders ?? 0,
            CancelledOrders = dashboard?.CancelledOrders ?? 0,
            TotalReviews = reviewStats?.TotalReviews ?? 0,
            AverageRating = reviewStats?.AverageRating ?? 0m,
            FiveStarReviews = reviewStats?.FiveStarReviews ?? 0,
            OneStarReviews = reviewStats?.OneStarReviews ?? 0,
            TotalSales = dashboard?.TotalRevenue ?? 0m,
            Currency = dashboard?.Currency ?? "CNY",
            LastUpdatedAt = dashboard?.LastUpdatedAt ?? now,
            IndexedAt = now,
            SchemaVersion = 2
        };

        return readModel;
    }
}
