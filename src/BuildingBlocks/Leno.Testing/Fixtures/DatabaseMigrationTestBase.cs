using Leno.Infrastructure.Persistence;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace Leno.Testing.Fixtures;

/// <summary>
/// 数据库迁移集成测试基类：基于 ContainerFixture 启动真实 SQL Server + Redis 容器，
/// 子类继承并指定具体 DbContext 类型，验证 MigrateWithLockAsync 在空库上完整创建 schema。
/// </summary>
[Collection(ContainerCollection.Name)]
public abstract class DatabaseMigrationTestBase<TDbContext> : IAsyncLifetime
    where TDbContext : DbContext
{
    protected readonly ContainerFixture Fixture;

    protected DatabaseMigrationTestBase(ContainerFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // 确保 Redis 容器已连接，注册 IDistributedLockProvider
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(Fixture.RedisConnectionString);
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConnectionMultiplexer>(_ => multiplexer);
        // 注: DistributedLock.Redis 1.1.1 不携带 AddDistributedRedisLock IServiceCollection 扩展方法（该扩展属于 DistributedLock 2.x）；
        // 此处与生产代码 AddRedis 一致采用手动注册 IDistributedLockProvider。
        services.AddSingleton<IDistributedLockProvider>(_ => new RedisDistributedSynchronizationProvider(multiplexer.GetDatabase()));

        // 子类配置 DbContext
        ConfigureServices(services, Fixture.SqlConnectionString);

        var provider = services.BuildServiceProvider();
        await provider.MigrateWithLockAsync<TDbContext>();
        Provider = provider;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected IServiceProvider Provider { get; private set; } = null!;

    protected abstract void ConfigureServices(IServiceCollection services, string sqlConnectionString);
}
