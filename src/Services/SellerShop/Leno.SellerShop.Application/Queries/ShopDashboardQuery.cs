namespace Leno.SellerShop.Application.Queries;

/// <summary>
/// 卖家工作台概览查询参数（CQRS 读侧 Query）。
/// 由 <see cref="ShopDashboardQueryHandler"/> 处理，经 <c>IShopDashboardReadModelAccessor</c> 走 ES 读模型。
/// 双发期 2 周内与 <c>SellerDashboardAppService.GetDashboardAsync</c> 并存，2 周后 Controller 切换到本 Query。
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
