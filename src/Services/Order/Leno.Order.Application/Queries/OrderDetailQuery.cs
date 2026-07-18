namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单详情查询参数（CQRS 读侧 Query）。
/// 由 <see cref="OrderDetailQueryHandler"/> 处理，经 <c>IOrderReadModelAccessor</c> 走 ES 读模型。
/// 双发期 2 周内与 <c>OrderAppService.GetByIdAsync</c> 并存，2 周后 Controller 切换到本 Query。
/// </summary>
public sealed class OrderDetailQuery
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; init; }

    /// <summary>当前用户标识，用于权限校验（买家仅能查自己的订单、卖家仅能查自己店铺的订单），可空表示系统内部调用。</summary>
    public Guid? CurrentUserId { get; init; }
}
