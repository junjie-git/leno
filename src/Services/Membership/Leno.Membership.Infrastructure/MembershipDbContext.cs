using Leno.Infrastructure.Persistence;
using Leno.Membership.Domain.Aggregates.Member;
using Leno.Membership.Domain.Aggregates.MemberLevelDefinition;
using Leno.Membership.Domain.Aggregates.MembershipPackage;
using Microsoft.EntityFrameworkCore;
using MemberAggregate = Leno.Membership.Domain.Aggregates.Member.Member;
using MemberLevelDefinitionAggregate = Leno.Membership.Domain.Aggregates.MemberLevelDefinition.MemberLevelDefinition;
using MembershipPackageAggregate = Leno.Membership.Domain.Aggregates.MembershipPackage.MembershipPackage;

namespace Leno.Membership.Infrastructure;

/// <summary>
/// Membership BC DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露会员、会员等级定义、会员套餐聚合与 OutboxMessage 发件箱表的 DbSet。
/// 数据库：membership_db（独立于旧 points_membership_db，支持 BC 独立伸缩与故障隔离）。
/// </summary>
public sealed class MembershipDbContext : BaseDbContext
{
    public MembershipDbContext(DbContextOptions<MembershipDbContext> options) : base(options)
    {
    }

    /// <summary>会员聚合根。</summary>
    public DbSet<MemberAggregate> Members => Set<MemberAggregate>();

    /// <summary>会员等级定义聚合根（运营配置）。</summary>
    public DbSet<MemberLevelDefinitionAggregate> MemberLevelDefinitions => Set<MemberLevelDefinitionAggregate>();

    /// <summary>会员套餐聚合根。</summary>
    public DbSet<MembershipPackageAggregate> MembershipPackages => Set<MembershipPackageAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MembershipDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
