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
}
