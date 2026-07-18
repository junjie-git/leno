namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单读模型访问器抽象（CQRS 读侧端口）。
/// 定义在 Application 层以保持分层洁癖：Application 不直接引用 Infrastructure 层的
/// <c>IEsReadModelRepository&lt;OrderReadModel&gt;</c>，由 Infrastructure 层实现。
/// </summary>
public interface IOrderReadModelAccessor
{
    /// <summary>
    /// 按订单标识查询 ES 读模型并映射为 <see cref="OrderDetailResult"/>。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>读模型存在则返回 <see cref="OrderDetailResult"/>，否则返回 null。</returns>
    Task<OrderDetailResult?> GetDetailAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 分页条件查询订单 ES 读模型并映射为 <see cref="OrderListResult"/>。
    /// </summary>
    /// <param name="query">列表查询参数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>分页结果，无命中时返回空列表与 0 总数。</returns>
    Task<OrderListResult> ListAsync(OrderListQuery query, CancellationToken ct = default);
}
