using Leno.PointsMembership.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;
using MemberAggregate = Leno.PointsMembership.Domain.Aggregates.Member;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 会员仓储接口，管理 <see cref="Member"/> 聚合。
/// </summary>
/// <remarks>
/// 双轨期弃用标记：此类型所属的 PointsMembership BC 已拆分为 Points BC + Membership BC（阶段四步骤 4.9）。
/// 新代码请使用 <c>Leno.Membership.Domain.Repositories.IMemberRepository</c>。
/// 双轨期 8 周后下线整个 PointsMembership BC。
/// </remarks>
[Obsolete("PointsMembership BC 已拆分为 Points BC + Membership BC（阶段四步骤 4.9）。双轨期 8 周后下线。新代码请使用 Leno.Membership.Domain.Repositories.IMemberRepository。", DiagnosticId = "LENO_PM_BC_SPLIT")]
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
