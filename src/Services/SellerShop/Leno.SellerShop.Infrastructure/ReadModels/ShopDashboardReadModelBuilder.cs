using Leno.SellerShop.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.ReadModels;

/// <summary>
/// <see cref="IShopDashboardReadModelBuilder"/> 默认实现。
/// 注入 SellerShop BC 既有仓储（<see cref="IShopRepository"/>、<see cref="IShopDashboardRepository"/>）查询最新聚合根，
/// 投影为 <see cref="ShopDashboardReadModel"/>；店铺不存在时返回 null。
/// 评论统计字段（TotalReviews/AverageRating/FiveStarReviews/OneStarReviews）暂以 0 占位，
/// 待后续接通 ReviewAfterSales BC 评论仓储后填充。
/// </summary>
public sealed class ShopDashboardReadModelBuilder : IShopDashboardReadModelBuilder
{
    private readonly IShopRepository _shopRepository;
    private readonly IShopDashboardRepository _dashboardRepository;
    private readonly ILogger<ShopDashboardReadModelBuilder> _logger;

    public ShopDashboardReadModelBuilder(
        IShopRepository shopRepository,
        IShopDashboardRepository dashboardRepository,
        ILogger<ShopDashboardReadModelBuilder> logger)
    {
        _shopRepository = shopRepository;
        _dashboardRepository = dashboardRepository;
        _logger = logger;
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

        var now = DateTime.UtcNow;
        var readModel = new ShopDashboardReadModel
        {
            ShopId = shop.Id,
            ShopName = shop.ShopName,
            TotalOrders = dashboard?.TotalOrders ?? 0,
            PendingOrders = dashboard?.PendingOrders ?? 0,
            // 当前 ShopDashboardData 聚合未细分 ConfirmedOrders/CancelledOrders，按 0 占位
            ConfirmedOrders = 0,
            CompletedOrders = dashboard?.CompletedOrders ?? 0,
            CancelledOrders = 0,
            // 评论统计暂以 0 占位：SellerShop BC 未持有评论仓储，待后续接通后填充
            TotalReviews = 0,
            AverageRating = 0m,
            FiveStarReviews = 0,
            OneStarReviews = 0,
            TotalSales = dashboard?.TotalRevenue ?? 0m,
            Currency = dashboard?.Currency ?? "CNY",
            LastUpdatedAt = dashboard?.LastUpdatedAt ?? now,
            IndexedAt = now,
            SchemaVersion = 1
        };

        return readModel;
    }
}
