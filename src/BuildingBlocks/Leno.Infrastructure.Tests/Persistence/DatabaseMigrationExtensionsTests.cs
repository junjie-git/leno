using FluentAssertions;
using Leno.Infrastructure.Persistence;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Leno.Infrastructure.Tests.Persistence;

public class DatabaseMigrationExtensionsTests
{
    [Fact]
    public async Task MigrateWithLockAsync_AcquiresLock_AndCallsMigrateAsync()
    {
        // Arrange
        var migrated = false;
        var dbContextMock = new Mock<TestDbContext>();
        dbContextMock
            .Setup(d => d.Database.MigrateAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                migrated = true;
                return Task.CompletedTask;
            })
            .Verifiable();

        var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
        var services = new ServiceCollection();
        services.AddSingleton<TestDbContext>(_ => dbContextMock.Object);
        // 注: DistributedLock.Redis 1.1.1 不携带 AddDistributedRedisLock IServiceCollection 扩展方法（该扩展属于 DistributedLock 2.x）；
        // 此处与生产代码 AddRedis 一致采用手动注册 IDistributedLockProvider。
        services.AddSingleton<IDistributedLockProvider>(_ => new RedisDistributedSynchronizationProvider(multiplexer.GetDatabase()));

        var provider = services.BuildServiceProvider();

        // Act
        await provider.MigrateWithLockAsync<TestDbContext>();

        // Assert
        migrated.Should().BeTrue("MigrateAsync 必须在获取锁后被调用");
        dbContextMock.Verify();
    }

    [Fact]
    public async Task MigrateWithLockAsync_LockAlreadyHeld_ShouldSkipMigrate()
    {
        // Arrange：先占用同一把锁，第二次调用应跳过 MigrateAsync
        var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
        var services = new ServiceCollection();
        var migrated = false;
        var dbContextMock = new Mock<TestDbContext>();
        dbContextMock
            .Setup(d => d.Database.MigrateAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                migrated = true;
                return Task.CompletedTask;
            });
        services.AddSingleton<TestDbContext>(_ => dbContextMock.Object);
        services.AddSingleton<IDistributedLockProvider>(_ => new RedisDistributedSynchronizationProvider(multiplexer.GetDatabase()));
        var provider = services.BuildServiceProvider();

        var lockProvider = provider.GetRequiredService<IDistributedLockProvider>();
        var lockKey = $"db-migrate:{typeof(TestDbContext).Name}";
        await using var heldHandle = await lockProvider.TryAcquireLockAsync(lockKey, TimeSpan.FromMinutes(1), CancellationToken.None);

        // Act：heldHandle 仍占用锁，MigrateWithLockAsync 应获取失败并跳过 MigrateAsync
        await provider.MigrateWithLockAsync<TestDbContext>(TimeSpan.FromSeconds(2));

        // Assert
        migrated.Should().BeFalse("锁已被占用时应跳过 MigrateAsync");
    }

    public abstract class TestDbContext : DbContext
    {
        public abstract new DatabaseFacade Database { get; }
    }
}
