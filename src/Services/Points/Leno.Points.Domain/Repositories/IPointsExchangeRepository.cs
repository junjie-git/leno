using Leno.Points.Domain.Aggregates.PointsExchange;
using Leno.SharedKernel.Abstractions;
using PointsExchangeAggregate = Leno.Points.Domain.Aggregates.PointsExchange.PointsExchange;

namespace Leno.Points.Domain.Repositories;

/// <summary>
/// 积分兑换仓储接口，管理 <see cref="PointsExchange"/> 聚合。
/// </summary>
public interface IPointsExchangeRepository : IRepository<PointsExchangeAggregate>
{
    /// <summary>
    /// 按用户标识查询兑换记录，按发起时间倒序。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    Task<List<PointsExchangeAggregate>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 按兑换目标标识查询兑换记录，用于幂等校验（防止重复兑换）。
    /// </summary>
    /// <param name="targetId">兑换目标标识。</param>
    /// <param name="userId">用户标识。</param>
    Task<PointsExchangeAggregate?> GetByTargetAsync(Guid targetId, Guid userId, CancellationToken ct = default);
}
