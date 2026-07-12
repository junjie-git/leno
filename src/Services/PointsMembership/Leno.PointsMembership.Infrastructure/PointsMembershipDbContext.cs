using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.PointsMembership.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.PointsMembership.Infrastructure;

/// <summary>
/// 积分会员域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与软删除查询过滤器。
/// 暴露积分账户、冻结明细、积分流水、签到记录、会员、会员等级、会员套餐、用户会员权益聚合与 OutboxMessage 发件箱表的 DbSet。
/// </summary>
public sealed class PointsMembershipDbContext : BaseDbContext
{
    public PointsMembershipDbContext(DbContextOptions<PointsMembershipDbContext> options) : base(options)
    {
    }

    /// <summary>积分账户聚合根。</summary>
    public DbSet<PointsAccount> PointsAccounts => Set<PointsAccount>();

    /// <summary>积分冻结明细子实体，隶属积分账户聚合。</summary>
    public DbSet<PointsFrozenEntry> PointsFrozenEntries => Set<PointsFrozenEntry>();

    /// <summary>积分流水实体，记录账户单笔变动明细。</summary>
    public DbSet<PointsLedger> PointsLedgers => Set<PointsLedger>();

    /// <summary>签到记录聚合根。</summary>
    public DbSet<CheckInRecord> CheckInRecords => Set<CheckInRecord>();

    /// <summary>会员聚合根。</summary>
    public DbSet<Member> Members => Set<Member>();

    /// <summary>会员等级聚合根。</summary>
    public DbSet<MembershipLevel> MembershipLevels => Set<MembershipLevel>();

    /// <summary>会员等级（成长值体系）聚合根。</summary>
    public DbSet<MemberLevel> MemberLevels => Set<MemberLevel>();

    /// <summary>会员套餐聚合根。</summary>
    public DbSet<MembershipPackage> MembershipPackages => Set<MembershipPackage>();

    /// <summary>用户会员权益聚合根。</summary>
    public DbSet<UserMembership> UserMemberships => Set<UserMembership>();

    /// <summary>发件箱消息表，与聚合变更同事务写入。</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}
