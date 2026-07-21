using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using RefundOrderAggregate = Leno.Payment.Domain.Aggregates.RefundOrder;

namespace Leno.Payment.Domain.Repositories;

/// <summary>
/// 退款单仓储接口，管理 <see cref="Aggregates.RefundOrder"/> 聚合。
/// 继承 <see cref="IRepository{T}"/> 获得 GetByIdAsync/AddAsync/UpdateAsync/RemoveAsync 基础能力。
/// </summary>
public interface IRefundOrderRepository : IRepository<RefundOrderAggregate>
{
    /// <summary>
    /// 按商户退款单号查询退款单。
    /// </summary>
    /// <param name="outRefundNo">商户退款单号。</param>
    Task<RefundOrderAggregate?> GetByOutRefundNoAsync(string outRefundNo, CancellationToken ct = default);

    /// <summary>
    /// 按售后单标识查询退款单。
    /// </summary>
    /// <param name="afterSalesId">售后单标识。</param>
    Task<RefundOrderAggregate?> GetByAfterSalesIdAsync(Guid afterSalesId, CancellationToken ct = default);

    /// <summary>
    /// 按订单标识查询退款单。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    Task<RefundOrderAggregate?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 分页条件查询退款单列表。
    /// </summary>
    /// <param name="orderId">订单标识过滤，为空不过滤。</param>
    /// <param name="status">退款状态过滤，为空不过滤。</param>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    Task<List<RefundOrderAggregate>> QueryAsync(Guid? orderId, RefundStatus? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 条件查询退款单总数（配合 <see cref="QueryAsync"/> 分页）。
    /// </summary>
    Task<int> CountAsync(Guid? orderId, RefundStatus? status, CancellationToken ct = default);

    /// <summary>
    /// 查询指定支付单关联的已退款成功（<see cref="RefundStatus.Succeeded"/>）的退款单列表。
    /// 用于内部查询服务汇总已退款金额。
    /// </summary>
    /// <param name="paymentId">关联支付单标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<RefundOrderAggregate>> GetSuccessfulRefundsByPaymentIdAsync(Guid paymentId, CancellationToken ct = default);
}
