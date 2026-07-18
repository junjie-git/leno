using Leno.Infrastructure.Abstractions.Cqrs;

namespace Leno.SellerShop.Application.Queries;

/// <summary>
/// 卖家工作台概览查询处理器。
/// 经 <see cref="IShopDashboardReadModelAccessor"/>（端口由 Infrastructure 层 <c>ShopDashboardReadModelAccessor</c> 实现）
/// 查询 ES 读模型并返回 <see cref="ShopDashboardResult"/>。店铺不存在（ES 中无对应文档）时返回 null。
/// 双发期 2 周内与 <c>SellerDashboardAppService.GetDashboardAsync</c> 并存，2 周后 Controller 切换到本 QueryHandler。
/// </summary>
public sealed class ShopDashboardQueryHandler : IQueryHandler<ShopDashboardQuery, ShopDashboardResult?>
{
    private readonly IShopDashboardReadModelAccessor _readModelAccessor;

    public ShopDashboardQueryHandler(IShopDashboardReadModelAccessor readModelAccessor)
    {
        ArgumentNullException.ThrowIfNull(readModelAccessor);
        _readModelAccessor = readModelAccessor;
    }

    /// <inheritdoc />
    public Task<ShopDashboardResult?> HandleAsync(ShopDashboardQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // StartDate/EndDate 当前为预留扩展点；当前读模型为快照型，暂不消费。
        _ = query.StartDate;
        _ = query.EndDate;

        return _readModelAccessor.GetByShopIdAsync(query.ShopId, ct);
    }
}
