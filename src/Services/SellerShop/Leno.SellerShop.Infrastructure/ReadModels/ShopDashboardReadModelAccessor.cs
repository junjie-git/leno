using Leno.Infrastructure.ReadModel;
using Leno.SellerShop.Application.Queries;

namespace Leno.SellerShop.Infrastructure.ReadModels;

/// <summary>
/// 店铺工作台读模型访问器实现，基于 <see cref="IEsReadModelRepository{T}"/> 查询 ES 读模型。
/// 实现 Application 层定义的 <see cref="IShopDashboardReadModelAccessor"/> 端口，保持分层洁癖。
/// 查询索引 <see cref="ShopDashboardReadModel.ShopDashboardIndexName"/>（leno_shop_dashboards）。
/// </summary>
public sealed class ShopDashboardReadModelAccessor : IShopDashboardReadModelAccessor
{
    private readonly IEsReadModelRepository<ShopDashboardReadModel> _repository;

    public ShopDashboardReadModelAccessor(IEsReadModelRepository<ShopDashboardReadModel> repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<ShopDashboardResult?> GetByShopIdAsync(Guid shopId, CancellationToken ct = default)
    {
        if (shopId == Guid.Empty)
        {
            return null;
        }

        var model = await _repository.GetByIdAsync(
            shopId.ToString(),
            ShopDashboardReadModel.ShopDashboardIndexName,
            ct);

        return model is null ? null : ToResult(model);
    }

    private static ShopDashboardResult ToResult(ShopDashboardReadModel model)
        => new()
        {
            ShopId = model.ShopId,
            ShopName = model.ShopName,
            TotalOrders = model.TotalOrders,
            PendingOrders = model.PendingOrders,
            ConfirmedOrders = model.ConfirmedOrders,
            CompletedOrders = model.CompletedOrders,
            CancelledOrders = model.CancelledOrders,
            TotalReviews = model.TotalReviews,
            AverageRating = model.AverageRating,
            FiveStarReviews = model.FiveStarReviews,
            OneStarReviews = model.OneStarReviews,
            TotalSales = model.TotalSales,
            Currency = model.Currency,
            LastUpdatedAt = model.LastUpdatedAt,
            IndexedAt = model.IndexedAt
        };
}
