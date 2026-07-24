using Leno.Membership.Domain.Aggregates.Member;
using Leno.SharedKernel.Abstractions;
using MemberAggregate = Leno.Membership.Domain.Aggregates.Member.Member;

namespace Leno.Membership.Domain.Repositories;

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

    /// <summary>
    /// 分页查询所有活跃会员，用于成长值等级定时评估任务。
    /// </summary>
    /// <param name="skip">跳过的记录数。</param>
    /// <param name="take">每次取回的记录数。</param>
    Task<List<MemberAggregate>> GetAllActiveAsync(int skip, int take, CancellationToken ct = default);
}
