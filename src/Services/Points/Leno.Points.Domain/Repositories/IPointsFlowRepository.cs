using Leno.Points.Domain.Aggregates.PointsFlow;
using Leno.SharedKernel.Abstractions;
using PointsFlowAggregate = Leno.Points.Domain.Aggregates.PointsFlow.PointsFlow;

namespace Leno.Points.Domain.Repositories;

/// <summary>
/// 积分流水仓储接口，管理 <see cref="PointsFlow"/> 流水实体查询。
/// 流水由聚合根同事务追加，仓储仅暴露查询能力，不暴露直接写入。
/// </summary>
public interface IPointsFlowRepository
{
    /// <summary>
    /// 按账户标识查询全部流水，按发生时间倒序。
    /// </summary>
    /// <param name="accountId">积分账户标识。</param>
    Task<List<PointsFlowAggregate>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// 按账户标识查询 Earn 类型的流水，按发生时间升序（FIFO），用于积分过期计算。
    /// </summary>
    /// <param name="accountId">积分账户标识。</param>
    Task<List<PointsFlowAggregate>> GetEarnFlowsByAccountIdAsync(Guid accountId, CancellationToken ct = default);
}
