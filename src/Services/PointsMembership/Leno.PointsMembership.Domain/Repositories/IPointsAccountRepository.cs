using Leno.PointsMembership.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;
using PointsAccountAggregate = Leno.PointsMembership.Domain.Aggregates.PointsAccount;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 积分账户仓储接口，管理 <see cref="PointsAccount"/> 聚合。
/// </summary>
public interface IPointsAccountRepository : IRepository<PointsAccountAggregate>
{
    /// <summary>
    /// 按用户标识查询积分账户。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    Task<PointsAccountAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 按冻结明细关联的订单标识查询积分账户，用于支付成功核销冻结或订单取消回退释放冻结。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    Task<PointsAccountAggregate?> GetByFrozenOrderIdAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 分页查询所有可用余额大于 0 的积分账户，用于积分过期定时任务。
    /// </summary>
    /// <param name="skip">跳过的记录数。</param>
    /// <param name="take">每次取回的记录数。</param>
    Task<List<PointsAccountAggregate>> GetAllWithPositiveBalanceAsync(int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// 按账户标识查询 Earn 类型的积分流水，按发生时间升序（FIFO），用于积分过期计算。
    /// </summary>
    /// <param name="accountId">积分账户标识。</param>
    Task<List<PointsLedger>> GetEarnLedgersByAccountIdAsync(Guid accountId, CancellationToken ct = default);
}
