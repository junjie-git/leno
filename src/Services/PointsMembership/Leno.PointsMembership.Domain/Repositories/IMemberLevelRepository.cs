using Leno.PointsMembership.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;
using MemberLevelAggregate = Leno.PointsMembership.Domain.Aggregates.MemberLevel;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 会员等级（成长值体系）仓储接口，管理 <see cref="MemberLevel"/> 聚合。
/// </summary>
public interface IMemberLevelRepository : IRepository<MemberLevelAggregate>
{
    /// <summary>
    /// 按等级编号查询会员等级定义。
    /// </summary>
    /// <param name="level">等级编号（0-4）。</param>
    Task<MemberLevelAggregate?> GetByLevelAsync(int level, CancellationToken ct = default);

    /// <summary>
    /// 查询所有会员等级，按 MinGrowthValue 升序，供等级评估。
    /// </summary>
    Task<List<MemberLevelAggregate>> GetAllAsync(CancellationToken ct = default);
}