using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.SystemAdmin.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure;

/// <summary>
/// 系统管理域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与配置自动发现。
/// </summary>
public sealed class SystemAdminDbContext : BaseDbContext
{
    public SystemAdminDbContext(DbContextOptions<SystemAdminDbContext> options) : base(options)
    {
    }

    /// <summary>运营人员聚合根。</summary>
    public DbSet<Operator> Operators => Set<Operator>();

    /// <summary>系统配置聚合根。</summary>
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();

    /// <summary>数据字典聚合根。</summary>
    public DbSet<DataDictionary> DataDictionaries => Set<DataDictionary>();

    /// <summary>审计日志聚合根。</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>操作日志聚合根。</summary>
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();

    /// <summary>系统公告聚合根。</summary>
    public DbSet<SystemAnnouncement> SystemAnnouncements => Set<SystemAnnouncement>();

    /// <summary>特性开关聚合根。</summary>
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    /// <summary>定时任务聚合根。</summary>
    public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();

    /// <summary>发件箱消息表。</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}
