using Leno.AfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using AfterSalesAggregate = Leno.AfterSales.Domain.Aggregates.AfterSalesOrder;

namespace Leno.AfterSales.Domain.Repositories;

/// <summary>
/// 售后单仓储接口，管理 <see cref="Aggregates.AfterSalesOrder"/> 聚合。
/// 继承 <see cref="IRepository{T}"/> 获得 GetByIdAsync/AddAsync/UpdateAsync/RemoveAsync 基础能力。
/// </summary>
public interface IAfterSalesRepository : IRepository<AfterSalesAggregate>
{
    /// <summary>
    /// 按订单标识查询该订单下的售后单列表。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    Task<List<AfterSalesAggregate>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 判断同订单行是否存在进行中的同类型售后单。
    /// </summary>
    /// <param name="orderLineId">订单行标识。</param>
    /// <param name="type">售后类型。</param>
    Task<bool> HasActiveByOrderLineAsync(Guid orderLineId, AfterSalesType type, CancellationToken ct = default);

    /// <summary>
    /// 判断同订单（整单售后，orderLineId 为 null）是否存在进行中的同类型售后单。
    /// 合并审计 3.3：补全整单售后重复申请校验。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="type">售后类型。</param>
    Task<bool> HasActiveByOrderAsync(Guid orderId, AfterSalesType type, CancellationToken ct = default);

    /// <summary>
    /// 分页条件查询售后单列表。
    /// </summary>
    /// <param name="orderId">订单标识过滤，为空不过滤。</param>
    /// <param name="userId">申请人标识过滤，为空不过滤。</param>
    /// <param name="sellerId">卖家标识过滤，为空不过滤。</param>
    /// <param name="status">售后状态过滤，为空不过滤。</param>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    Task<List<AfterSalesAggregate>> QueryAsync(Guid? orderId, Guid? userId, Guid? sellerId, AfterSalesStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 条件查询售后单总数（配合 <see cref="QueryAsync"/> 分页）。
    /// </summary>
    Task<int> CountAsync(Guid? orderId, Guid? userId, Guid? sellerId, AfterSalesStatus? status, CancellationToken ct = default);
}
