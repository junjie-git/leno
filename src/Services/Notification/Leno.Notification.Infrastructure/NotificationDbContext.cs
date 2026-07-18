using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.Notification.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Leno.Notification.Infrastructure;

/// <summary>
/// 通知域 DbContext，继承 <see cref="BaseDbContext"/> 复用审计字段填充与配置自动发现。
/// </summary>
public sealed class NotificationDbContext : BaseDbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    /// <summary>通知记录聚合根。</summary>
    public DbSet<NotificationRecord> NotificationRecords => Set<NotificationRecord>();

    /// <summary>通知模板聚合根。</summary>
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    /// <summary>通知偏好聚合根。</summary>
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
}
