using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// 数据库迁移扩展方法，基于 Redis 分布式锁避免多实例并发执行 EF Core 迁移导致 schema 冲突。
/// 在各 BC Program.cs 中 <c>app.Run()</c> 前调用 <c>await app.Services.MigrateWithLockAsync&lt;XxxDbContext&gt;()</c>。
/// <para>
/// <b>执行点策略（N4 / 2026-09-21）</b>：schema 的事实来源已统一为 EF Core 迁移（N1），
/// 而"在哪儿执行"按环境区分，避免部署期 Job 与启动期迁移两条路径重复搬运同一批迁移：
/// </para>
/// <list type="number">
/// <item>显式配置 <see cref="MigrateOnStartupConfigKey"/>（bool）存在时以其为准 —— 运维逃生舱，
///   可强制开启或强制关闭；</item>
/// <item>否则仅 <c>Development</c> 环境执行 —— 本地开发无需先跑 Helm Job 即可建表；</item>
/// <item>其余环境（Docker / Staging / Production）<b>跳过</b>，由部署期
///   <c>helm migration-job</c>（pre-install/pre-upgrade hook 执行 <c>files/migrations/*.sql</c>）负责。</item>
/// </list>
/// <para>
/// 若解析不到 <see cref="IHostEnvironment"/>（单元测试、非 Host 场景），因无法判定环境，
/// 保持既有行为 —— 执行迁移。
/// </para>
/// </summary>
public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// 是否允许在应用启动时执行迁移的配置键（可空 bool）。
    /// 未设置时按环境判定：Development 执行，其余环境跳过（详见类型注释）。
    /// </summary>
    public const string MigrateOnStartupConfigKey = "Database:MigrateOnStartup";

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
        var logger = sp.GetService<ILoggerFactory>()?.CreateLogger(typeof(DatabaseMigrationExtensions).FullName ?? "DatabaseMigration");

        // N4：先判环境，再取锁 —— 跳过的环境不应消耗分布式锁，也不应触发任何 DB 往返。
        if (!ShouldMigrateOnStartup(sp, logger, typeof(TDbContext).Name))
        {
            return;
        }

        var db = sp.GetRequiredService<TDbContext>();
        var lockProvider = sp.GetRequiredService<IDistributedLockProvider>();

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

    /// <summary>
    /// 判定本次启动是否应执行迁移。判定优先级见类型注释。
    /// </summary>
    /// <param name="sp">当前作用域的服务提供者。</param>
    /// <param name="logger">日志记录器，可为 null。</param>
    /// <param name="dbContextName">DbContext 类型名，仅用于日志。</param>
    /// <returns>true 表示应在启动时执行迁移。</returns>
    private static bool ShouldMigrateOnStartup(IServiceProvider sp, ILogger? logger, string dbContextName)
    {
        // 1) 显式配置优先（运维逃生舱）：存在时不再判定环境
        var configured = sp.GetService<IConfiguration>()?.GetValue<bool?>(MigrateOnStartupConfigKey);
        if (configured.HasValue)
        {
            logger?.LogInformation(
                "{DbContextName} 迁移：由配置 {ConfigKey}={Value} 显式决定（跳过环境判定）",
                dbContextName, MigrateOnStartupConfigKey, configured.Value);
            return configured.Value;
        }

        // 2) 无宿主环境（单元测试 / 非 Host 场景）：无法判定，保持既有行为
        var environment = sp.GetService<IHostEnvironment>();
        if (environment is null)
        {
            return true;
        }

        // 3) Development 执行；其余环境交由部署期迁移 Job
        if (environment.IsDevelopment())
        {
            return true;
        }

        logger?.LogInformation(
            "{DbContextName} 迁移：当前环境 {EnvironmentName} 不在应用启动时执行迁移，schema 由部署期迁移 Job（helm migration-job）负责。如需覆盖请设置 {ConfigKey}=true",
            dbContextName, environment.EnvironmentName, MigrateOnStartupConfigKey);
        return false;
    }
}
