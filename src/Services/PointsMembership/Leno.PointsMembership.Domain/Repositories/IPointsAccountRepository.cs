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
}
