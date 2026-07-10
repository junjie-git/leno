using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MembershipPackageAggregate = Leno.PointsMembership.Domain.Aggregates.MembershipPackage;

namespace Leno.PointsMembership.Domain.Repositories;

/// <summary>
/// 会员套餐仓储接口，管理 <see cref="MembershipPackage"/> 聚合。
/// </summary>
public interface IMembershipPackageRepository : IRepository<MembershipPackageAggregate>
{
    /// <summary>
    /// 查询所有已启用的会员套餐，供买家购买页展示。
    /// </summary>
    Task<List<MembershipPackageAggregate>> GetAllEnabledAsync(CancellationToken ct = default);
}
