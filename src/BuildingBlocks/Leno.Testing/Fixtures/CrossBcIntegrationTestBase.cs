using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace Leno.Testing.Fixtures;

/// <summary>
/// 跨 BC 集成测试基类：基于 ContainerFixture 启动真实 Testcontainers（MsSql + Redis + RabbitMq），
/// 提供 MassTransit InMemoryTestHarness 或 RabbitMqTestHarness 选项，
/// 子类注册具体 DbContext 与消费者，验证跨 BC 事件流转。
/// 所有测试方法自动标记 [Trait("Category", "Integration")]（通过 Assembly 属性或基类 Trait）。
/// </summary>
[Collection(ContainerCollection.Name)]
[Trait("Category", "Integration")]
public abstract class CrossBcIntegrationTestBase<TDbContext> : IAsyncLifetime
    where TDbContext : DbContext
{
    protected readonly ContainerFixture Fixture;
    protected IServiceProvider ServiceProvider { get; private set; } = null!;
    protected ITestHarness TestHarness { get; private set; } = null!;

    protected CrossBcIntegrationTestBase(ContainerFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(Fixture.RedisConnectionString);
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddDebug());

        // 注册 Redis 与分布式锁
        services.AddSingleton<IConnectionMultiplexer>(_ => multiplexer);
        // 注: DistributedLock.Redis 1.1.1 不携带 AddDistributedRedisLock IServiceCollection 扩展方法（该扩展属于 DistributedLock 2.x）；
        // 此处与生产代码 AddRedis 及 DatabaseMigrationTestBase 一致采用手动注册 IDistributedLockProvider。
        services.AddSingleton<IDistributedLockProvider>(_ => new RedisDistributedSynchronizationProvider(multiplexer.GetDatabase()));
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();

        // MassTransit Test Harness（连接到 Testcontainers RabbitMq）
        services.AddMassTransitTestHarness(cfg =>
        {
            ConfigureConsumers(cfg);
        });

        // 子类注册 DbContext 与其他服务
        ConfigureServices(services, Fixture.SqlConnectionString, Fixture.RabbitMqConnectionString);

        ServiceProvider = services.BuildServiceProvider();

        // 执行迁移
        await ServiceProvider.MigrateWithLockAsync<TDbContext>();

        // 启动 MassTransit Test Harness
        TestHarness = ServiceProvider.GetRequiredService<ITestHarness>();
        await TestHarness.Start();
    }

    public async Task DisposeAsync()
    {
        if (TestHarness is not null)
        {
            await TestHarness.Stop();
        }
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        await Task.CompletedTask;
    }

    protected abstract void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString);

    protected abstract void ConfigureConsumers(IBusRegistrationConfigurator configurator);
}
