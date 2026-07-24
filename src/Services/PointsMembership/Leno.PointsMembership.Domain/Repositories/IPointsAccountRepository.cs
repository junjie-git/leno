using Leno.PointsMembership.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;
using PointsAccountAggregate = Leno.PointsMembership.Domain.Aggregates.PointsAccount;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 积分账户仓储接口，管理 <see cref="PointsAccount"/> 聚合。
/// </summary>
/// <remarks>
/// 双轨期弃用标记：此类型所属的 PointsMembership BC 已拆分为 Points BC + Membership BC（阶段四步骤 4.9）。
/// 新代码请使用 <c>Leno.Points.Domain.Repositories.IPointsAccountRepository</c>。
/// 双轨期 8 周后下线整个 PointsMembership BC。
/// </remarks>
[Obsolete("PointsMembership BC 已拆分为 Points BC + Membership BC（阶段四步骤 4.9）。双轨期 8 周后下线。新代码请使用 Leno.Points.Domain.Repositories.IPointsAccountRepository。", DiagnosticId = "LENO_PM_BC_SPLIT")]
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

    /// <summary>
    /// 按用户标识分页查询积分流水，按发生时间倒序（最新在前）。
    /// PM-M07 修复：供 PointsAppService.GetLedgerAsync 真实分页查询使用，替代原先返回空列表的占位实现。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="page">页码，从 1 开始。</param>
    /// <param name="pageSize">每页条数。</param>
    Task<List<PointsLedger>> GetLedgersByUserIdAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
}
