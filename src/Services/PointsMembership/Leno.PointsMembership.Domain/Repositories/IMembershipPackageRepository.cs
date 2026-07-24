using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MembershipPackageAggregate = Leno.PointsMembership.Domain.Aggregates.MembershipPackage;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 会员套餐仓储接口，管理 <see cref="MembershipPackage"/> 聚合。
/// </summary>
/// <remarks>
/// 双轨期弃用标记：此类型所属的 PointsMembership BC 已拆分为 Points BC + Membership BC（阶段四步骤 4.9）。
/// 新代码请使用 <c>Leno.Membership.Domain.Repositories.IMembershipPackageRepository</c>。
/// 双轨期 8 周后下线整个 PointsMembership BC。
/// </remarks>
[Obsolete("PointsMembership BC 已拆分为 Points BC + Membership BC（阶段四步骤 4.9）。双轨期 8 周后下线。新代码请使用 Leno.Membership.Domain.Repositories.IMembershipPackageRepository。", DiagnosticId = "LENO_PM_BC_SPLIT")]
public interface IMembershipPackageRepository : IRepository<MembershipPackageAggregate>
{
    /// <summary>
    /// 查询所有已启用的会员套餐，供买家购买页展示。
    /// </summary>
    Task<List<MembershipPackageAggregate>> GetAllEnabledAsync(CancellationToken ct = default);
}
