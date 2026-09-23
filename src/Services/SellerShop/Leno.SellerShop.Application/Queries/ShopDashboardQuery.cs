namespace Leno.SellerShop.Application.Queries;

/// <summary>
/// 卖家工作台概览查询参数（CQRS 读侧 Query）。
/// 由 <see cref="ShopDashboardQueryHandler"/> 处理，经 <c>IShopDashboardReadModelAccessor</c> 走 ES 读模型。
/// 双轨下线 E2（2026-09-23）：本 Query 为工作台概览的唯一读路径（SQL 双轨已删除）。
/// </summary>
public sealed class ShopDashboardQuery
{
    /// <summary>店铺标识（语义等同订单域 SellerId）。</summary>
    public Guid ShopId { get; init; }

    /// <summary>可选时间范围起始（UTC），预留扩展点；当前读模型为快照型，暂不消费。</summary>
    public DateTime? StartDate { get; init; }

    /// <summary>可选时间范围结束（UTC），预留扩展点；当前读模型为快照型，暂不消费。</summary>
    public DateTime? EndDate { get; init; }
}
