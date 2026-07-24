using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// 数据库迁移扩展方法，基于 Redis 分布式锁避免多实例并发执行 EF Core 迁移导致 schema 冲突。
/// 在各 BC Program.cs 中 <c>app.Run()</c> 前调用 <c>await app.Services.MigrateWithLockAsync&lt;XxxDbContext&gt;()</c>。
/// </summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// 在 Redis 分布式锁保护下执行 EF Core 数据库迁移。
    /// 同一 DbContext 类名的锁键（db-migrate:{DbContextName}）同一时刻仅允许一个实例执行迁移，
    /// 其他实例获取锁失败时直接跳过（已由首个实例完成迁移）。
    /// </summary>
    /// <typeparam name="TDbContext">业务上下文 DbContext 类型</typeparam>
    /// <param name="services">应用服务提供者</param>
    /// <param name="acquireTimeout">获取锁的最大等待时间，默认 5 分钟</param>
    /// <param name="ct">取消令牌</param>
    public static async Task MigrateWithLockAsync<TDbContext>(
        this IServiceProvider services,
        TimeSpan? acquireTimeout = null,
        CancellationToken ct = default)
        where TDbContext : DbContext
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<TDbContext>();
        var lockProvider = sp.GetRequiredService<IDistributedLockProvider>();
        var logger = sp.GetService<ILoggerFactory>()?.CreateLogger(typeof(DatabaseMigrationExtensions).FullName ?? "DatabaseMigration");

        var lockKey = $"db-migrate:{typeof(TDbContext).Name}";
        var timeout = acquireTimeout ?? TimeSpan.FromMinutes(5);

        await using var handle = await lockProvider.TryAcquireLockAsync(lockKey, timeout, ct);
        if (handle == null)
        {
            logger?.LogInformation("数据库迁移锁 {LockKey} 已被其他实例持有，跳过迁移", lockKey);
            return;
        }

        logger?.LogInformation("已获取迁移锁 {LockKey}，开始执行 {DbContextName} 迁移", lockKey, typeof(TDbContext).Name);
        await db.Database.MigrateAsync(ct);
        logger?.LogInformation("{DbContextName} 迁移完成", typeof(TDbContext).Name);
    }
}
