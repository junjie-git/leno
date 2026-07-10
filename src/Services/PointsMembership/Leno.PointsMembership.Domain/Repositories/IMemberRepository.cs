using Leno.PointsMembership.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;
using MemberAggregate = Leno.PointsMembership.Domain.Aggregates.Member;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 会员仓储接口，管理 <see cref="Member"/> 聚合。
/// </summary>
public interface IMemberRepository : IRepository<MemberAggregate>
{
    /// <summary>
    /// 按用户标识查询会员。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    Task<MemberAggregate?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
