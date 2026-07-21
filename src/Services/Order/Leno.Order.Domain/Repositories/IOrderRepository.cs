using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Domain.Repositories;

/// <summary>
/// 订单仓储接口，管理 <see cref="Aggregates.Order"/> 聚合。
/// 继承 <see cref="IRepository{T}"/> 获得 GetByIdAsync/AddAsync/UpdateAsync/RemoveAsync 基础能力。
/// </summary>
public interface IOrderRepository : IRepository<OrderAggregate>
{
    /// <summary>
    /// 按订单编号查询订单。
    /// </summary>
    /// <param name="orderNo">订单编号。</param>
    Task<OrderAggregate?> GetByOrderNoAsync(string orderNo, CancellationToken ct = default);

    /// <summary>
    /// 分页条件查询订单列表。
    /// </summary>
    /// <param name="userId">买家标识过滤，为空不过滤。</param>
    /// <param name="sellerId">卖家标识过滤，为空不过滤。</param>
    /// <param name="status">订单状态过滤，为空不过滤。</param>
    /// <param name="startDate">创建起始时间过滤，为空不过滤。</param>
    /// <param name="endDate">创建结束时间过滤，为空不过滤。</param>
    /// <param name="page">页码（从 0 起，P2-T35：与 CQRS OrderListQuery.PageIndex 对齐）。</param>
    /// <param name="pageSize">每页大小。</param>
    Task<List<OrderAggregate>> QueryAsync(Guid? userId, Guid? sellerId, OrderStatus? status, DateTime? startDate, DateTime? endDate, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 条件查询订单总数（配合 <see cref="QueryAsync"/> 分页）。
    /// </summary>
    Task<int> CountAsync(Guid? userId, Guid? sellerId, OrderStatus? status, DateTime? startDate, DateTime? endDate, CancellationToken ct = default);
}
