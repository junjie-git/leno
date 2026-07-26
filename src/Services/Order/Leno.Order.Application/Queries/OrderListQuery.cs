namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单列表分页查询参数（CQRS 读侧 Query）。
/// 由 <see cref="OrderListQueryHandler"/> 处理，经 <c>IOrderReadModelAccessor</c> 走 ES 读模型。
/// 双发期 2 周内与 <c>OrderAppService.QueryAsync</c> 并存，2 周后 Controller 切换到本 Query。
/// </summary>
public sealed class OrderListQuery
{
    /// <summary>买家标识过滤，可空表示不限。</summary>
    public Guid? UserId { get; init; }

    /// <summary>卖家（店铺）标识过滤，可空表示不限。</summary>
    public Guid? SellerId { get; init; }

    /// <summary>订单状态名称过滤（如 "Paid"、"Shipped"），可空表示不限。与 <c>OrderReadModel.Status</c> 字符串匹配。</summary>
    public string? Status { get; init; }

    /// <summary>订单号模糊搜索过滤，可空表示不限。非空时对 <c>OrderReadModel.OrderNo</c> 做 MatchQuery 模糊匹配。</summary>
    public string? OrderNo { get; init; }

    /// <summary>创建起始时间（UTC）过滤，可空表示不限。</summary>
    public DateTime? StartDate { get; init; }

    /// <summary>创建结束时间（UTC）过滤，可空表示不限。</summary>
    public DateTime? EndDate { get; init; }

    /// <summary>页码，从 0 起。</summary>
    public int PageIndex { get; init; }

    /// <summary>每页条数，默认 20。</summary>
    public int PageSize { get; init; } = 20;
}
