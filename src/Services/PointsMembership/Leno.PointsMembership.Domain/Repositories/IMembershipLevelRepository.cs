using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MembershipLevelAggregate = Leno.PointsMembership.Domain.Aggregates.MembershipLevel;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 会员等级仓储接口，管理 <see cref="MembershipLevel"/> 聚合。
/// </summary>
public interface IMembershipLevelRepository : IRepository<MembershipLevelAggregate>
{
    /// <summary>
    /// 按等级编号查询会员等级定义。
    /// </summary>
    /// <param name="level">等级编号。</param>
    Task<MembershipLevelAggregate?> GetByLevelAsync(int level, CancellationToken ct = default);

    /// <summary>
    /// 查询所有已启用的会员等级，按等级编号升序，供升级判定与展示。
    /// </summary>
    Task<List<MembershipLevelAggregate>> GetAllEnabledAsync(CancellationToken ct = default);
}
