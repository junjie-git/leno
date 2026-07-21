using Leno.Payment.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using PaymentOrderAggregate = Leno.Payment.Domain.Aggregates.PaymentOrder;

namespace Leno.Payment.Domain.Repositories;

/// <summary>
/// 支付单仓储接口，管理 <see cref="Aggregates.PaymentOrder"/> 聚合。
/// 继承 <see cref="IRepository{T}"/> 获得 GetByIdAsync/AddAsync/UpdateAsync/RemoveAsync 基础能力。
/// </summary>
public interface IPaymentOrderRepository : IRepository<PaymentOrderAggregate>
{
    /// <summary>
    /// 按商户支付单号查询支付单。
    /// </summary>
    /// <param name="outTradeNo">商户支付单号。</param>
    Task<PaymentOrderAggregate?> GetByOutTradeNoAsync(string outTradeNo, CancellationToken ct = default);

    /// <summary>
    /// 按订单标识查询支付单。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    Task<PaymentOrderAggregate?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 分页条件查询支付单列表。
    /// </summary>
    /// <param name="userId">买家标识过滤，为空不过滤。</param>
    /// <param name="channel">支付渠道过滤，为空不过滤。</param>
    /// <param name="status">支付状态过滤，为空不过滤。</param>
    /// <param name="startDate">创建起始时间过滤，为空不过滤。</param>
    /// <param name="endDate">创建结束时间过滤，为空不过滤。</param>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    Task<List<PaymentOrderAggregate>> QueryAsync(Guid? userId, PaymentChannel? channel, PaymentStatus? status, DateTime? startDate, DateTime? endDate, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 条件查询支付单总数（配合 <see cref="QueryAsync"/> 分页）。
    /// </summary>
    Task<int> CountAsync(Guid? userId, PaymentChannel? channel, PaymentStatus? status, DateTime? startDate, DateTime? endDate, CancellationToken ct = default);

    /// <summary>
    /// 按 <see cref="Aggregates.PaymentOrder.PaidAt"/>（支付成功时间）分页查询已支付支付单。
    /// 用于 T+1 对账场景：跨日支付（如 23:50 创建、次日 00:10 支付成功）应按 PaidAt
    /// 归入实际支付日对账范围，避免按 CreatedAt 过滤导致的漏对账。
    /// 实现应同时过滤 Status == <see cref="ValueObjects.PaymentStatus.Paid"/>。
    /// </summary>
    /// <param name="channel">支付渠道过滤，为空不过滤。</param>
    /// <param name="paidStart">PaidAt 起始时间（含，UTC）。</param>
    /// <param name="paidEnd">PaidAt 结束时间（含，UTC）。</param>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<PaymentOrderAggregate>> QueryPaidByPaidAtAsync(
        PaymentChannel? channel,
        DateTime paidStart,
        DateTime paidEnd,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
