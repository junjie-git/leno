using FluentAssertions;
using Leno.Infrastructure.Persistence;
using Medallion.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;namespace Leno.Infrastructure.Tests.Persistence;/// <summary>
/// MigrateWithLockAsync 单元测试。
/// 设计约束：不得依赖真实 Redis（CI 环境无 Redis，run #9 曾因
/// ConnectionMultiplexer.Connect("localhost:6379") 抛 RedisConnectionException 失败）；
/// 不得 mock DatabaseFacade 的扩展方法 MigrateAsync（Moq 对扩展方法抛 NotSupportedException）。
/// 方案：进程内 SemaphoreSlim 实现的假 IDistributedLockProvider + 真实 SQLite 内存库，
/// 以 __EFMigrationsHistory 表的存在性作为「MigrateAsync 是否被执行」的探针
/// （EF Core Migrate 即便无迁移也会确保创建迁移历史表）。
/// </summary>
public class DatabaseMigrationExtensionsTests
{
    [Fact]
    public async Task MigrateWithLockAsync_AcquiresLock_AndCallsMigrateAsync()
    {
        // Arrange：共享打开的 SQLite 连接（:memory: 库随连接关闭销毁，
        // 必须让 MigrateWithLockAsync 内部 scope 解析出的 context 与探针共用同一连接）
        await using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var services = new ServiceCollection();
        services.AddDbContext<MigrateTestDbContext>(o => o.UseSqlite(connection));
        services.AddSingleton<IDistributedLockProvider>(new SemaphoreSlimLockProvider());

        var provider = services.BuildServiceProvider();

        // Act
        await provider.MigrateWithLockAsync<MigrateTestDbContext>();

        // Assert：迁移历史表已创建，证明 MigrateAsync 在获取锁后被真实执行
        var historyTableExists = await HistoryTableExistsAsync(connection);
        historyTableExists.Should().BeTrue("MigrateAsync 必须在获取锁后被调用（历史表应被创建）");
    }

    [Fact]
    public async Task MigrateWithLockAsync_LockAlreadyHeld_ShouldSkipMigrate()
    {
        // Arrange：先占用同一把锁，第二次调用应跳过 MigrateAsync
        var lockProvider = new SemaphoreSlimLockProvider();
        await using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var services = new ServiceCollection();
        services.AddDbContext<MigrateTestDbContext>(o => o.UseSqlite(connection));
        services.AddSingleton<IDistributedLockProvider>(lockProvider);
        var provider = services.BuildServiceProvider();

        var lockKey = $"db-migrate:{typeof(MigrateTestDbContext).Name}";
        await using var heldHandle = await lockProvider.CreateLock(lockKey)
            .TryAcquireAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

        heldHandle.Should().NotBeNull("前置条件：测试自身应能获取到锁");

        // Act：heldHandle 仍占用锁，MigrateWithLockAsync 应获取失败并跳过 MigrateAsync
        await provider.MigrateWithLockAsync<MigrateTestDbContext>(TimeSpan.FromSeconds(2));

        // Assert：MigrateAsync 未执行 → 迁移历史表不应被创建
        var historyTableExists = await HistoryTableExistsAsync(connection);
        historyTableExists.Should().BeFalse("锁已被占用时应跳过 MigrateAsync（历史表不应被创建）");
    }

    // ===== N4：启动迁移的环境门控（2026-09-21） =====
    // 策略：显式配置 > 环境判定（Development 执行，其余跳过）> 无 IHostEnvironment 时保持既有行为

    /// <summary>Development 环境应执行启动迁移（本地开发无需先跑 Helm Job）。</summary>
    [Fact]
    public async Task MigrateWithLockAsync_DevelopmentEnvironment_ShouldRunMigrate()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var provider = BuildProvider(connection, new SemaphoreSlimLockProvider(), Environments.Development, null);

        await provider.MigrateWithLockAsync<MigrateTestDbContext>();

        (await HistoryTableExistsAsync(connection)).Should().BeTrue("Development 环境应在启动时执行迁移");
    }

    /// <summary>非 Development 环境应跳过启动迁移，交由部署期 migration-job 负责。</summary>
    [Theory]
    [InlineData("Docker")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task MigrateWithLockAsync_NonDevelopmentEnvironment_ShouldSkipMigrate(string environmentName)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var provider = BuildProvider(connection, new SemaphoreSlimLockProvider(), environmentName, null);

        await provider.MigrateWithLockAsync<MigrateTestDbContext>();

        (await HistoryTableExistsAsync(connection)).Should().BeFalse(
            $"{environmentName} 环境不应在启动时迁移（由部署期 migration-job 负责）");
    }

    /// <summary>配置显式开启（逃生舱）时，非 Development 环境也应执行迁移。</summary>
    [Fact]
    public async Task MigrateWithLockAsync_ConfigOverrideTrue_InNonDevelopment_ShouldRunMigrate()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var provider = BuildProvider(connection, new SemaphoreSlimLockProvider(), Environments.Production, true);

        await provider.MigrateWithLockAsync<MigrateTestDbContext>();

        (await HistoryTableExistsAsync(connection)).Should().BeTrue(
            "显式配置 Database:MigrateOnStartup=true 应覆盖环境判定");
    }

    /// <summary>配置显式关闭时，Development 环境也应跳过迁移。</summary>
    [Fact]
    public async Task MigrateWithLockAsync_ConfigOverrideFalse_InDevelopment_ShouldSkipMigrate()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var provider = BuildProvider(connection, new SemaphoreSlimLockProvider(), Environments.Development, false);

        await provider.MigrateWithLockAsync<MigrateTestDbContext>();

        (await HistoryTableExistsAsync(connection)).Should().BeFalse(
            "显式配置 Database:MigrateOnStartup=false 应覆盖环境判定");
    }

    /// <summary>
    /// 构建带环境名与可选迁移开关的测试 ServiceProvider。
    /// </summary>
    /// <param name="connection">共享的 SQLite 内存连接（探针与 DbContext 必须复用同一连接）。</param>
    /// <param name="lockProvider">进程内假分布式锁提供者。</param>
    /// <param name="environmentName">环境名；null 表示不注册 IHostEnvironment。</param>
    /// <param name="migrateOnStartup">Database:MigrateOnStartup 值；null 表示不注册该配置键。</param>
    private static ServiceProvider BuildProvider(
        SqliteConnection connection,
        IDistributedLockProvider lockProvider,
        string? environmentName,
        bool? migrateOnStartup)
    {
        var services = new ServiceCollection();
        services.AddDbContext<MigrateTestDbContext>(o => o.UseSqlite(connection));
        services.AddSingleton(lockProvider);

        if (environmentName is not null)
        {
            services.AddSingleton<IHostEnvironment>(new StubHostEnvironment(environmentName));
        }

        if (migrateOnStartup.HasValue)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [DatabaseMigrationExtensions.MigrateOnStartupConfigKey] = migrateOnStartup.Value ? "true" : "false"
                })
                .Build();
            services.AddSingleton<IConfiguration>(configuration);
        }

        return services.BuildServiceProvider();
    }

    /// <summary>最小 IHostEnvironment 替身（仅环境名参与门控判定）。</summary>
    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Leno.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static async Task<bool> HistoryTableExistsAsync(SqliteConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(CancellationToken.None);
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory'";
        var result = await command.ExecuteScalarAsync(CancellationToken.None);
        return Convert.ToInt64(result) > 0;
    }

    /// <summary>
    /// 进程内假分布式锁提供者：以 ConcurrentDictionary(name → SemaphoreSlim) 模拟
    /// 命名互斥锁，行为语义与 Redis 锁一致（同名字互斥、超时获取失败返回 null）。
    /// </summary>
    private sealed class SemaphoreSlimLockProvider : IDistributedLockProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

        public IDistributedLock CreateLock(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return new SemaphoreSlimLock(_gates.GetOrAdd(name, _ => new SemaphoreSlim(1, 1)));
        }
    }

    private sealed class SemaphoreSlimLock(SemaphoreSlim gate) : IDistributedLock
    {
        public string Name => "semaphore-slim-lock";

        // 注意：DistributedLock.Core 1.0.8 的接口签名为 TryAcquire(TimeSpan, ct)（非可空），
        // 与 AcquireAsync(TimeSpan?, ct)（可空）不对称，必须逐一对齐。
        public IDistributedSynchronizationHandle? TryAcquire(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return gate.Wait(timeout, cancellationToken)
                ? new SemaphoreSlimHandle(gate)
                : null;
        }

        public IDistributedSynchronizationHandle Acquire(TimeSpan? timeout = default, CancellationToken cancellationToken = default)
        {
            return TryAcquire(timeout ?? Timeout.InfiniteTimeSpan, cancellationToken)
                ?? throw new TimeoutException("未能获取分布式锁（SemaphoreSlim 测试替身）");
        }

        public async ValueTask<IDistributedSynchronizationHandle?> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return await gate.WaitAsync(timeout, cancellationToken)
                ? new SemaphoreSlimHandle(gate)
                : null;
        }

        public async ValueTask<IDistributedSynchronizationHandle> AcquireAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default)
        {
            return await TryAcquireAsync(timeout ?? Timeout.InfiniteTimeSpan, cancellationToken)
                ?? throw new TimeoutException("未能获取分布式锁（SemaphoreSlim 测试替身）");
        }
    }

    private sealed class SemaphoreSlimHandle(SemaphoreSlim gate) : IDistributedSynchronizationHandle
    {
        public CancellationToken HandleLostToken => CancellationToken.None;

        public void Dispose() => gate.Release();

        public ValueTask DisposeAsync()
        {
            gate.Release();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>迁移探针用空模型 SQLite 上下文（无实体，仅用于触发 MigrateAsync）。</summary>
    public sealed class MigrateTestDbContext : DbContext
    {
        public MigrateTestDbContext(DbContextOptions<MigrateTestDbContext> options) : base(options)
        {
        }
    }
}
