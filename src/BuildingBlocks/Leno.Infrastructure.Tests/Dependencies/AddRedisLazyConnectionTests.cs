using Leno.Infrastructure.Dependencies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Leno.Infrastructure.Tests.Dependencies;

/// <summary>
/// T21 验证：AddRedis 使用 Lazy&lt;Task&lt;IConnectionMultiplexer&gt;&gt; 延迟异步连接，
/// 注册阶段不触发 ConnectionMultiplexer.Connect 同步阻塞。
/// </summary>
public class AddRedisLazyConnectionTests
{
    [Fact]
    public void AddLenoInfrastructure_DoesNotConnectDuringRegistration()
    {
        // Arrange — 配置一个不存在的 Redis 地址
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "nonexistent-host:6379,abortConnect=false",
                ["Elasticsearch:Uri"] = "http://localhost:9200"
            })
            .Build();

        // Act — AddLenoInfrastructure 应在注册阶段不触发 Redis 连接（旧代码 Connect 会同步尝试连接）
        var act = () => services.AddLenoInfrastructure(config);

        // Assert — 注册阶段不应抛异常（连接推迟到首次解析 IConnectionMultiplexer）
        act.Should().NotThrow();
    }

    [Fact]
    public void AddLenoInfrastructure_RegistersIConnectionMultiplexerAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379,abortConnect=false",
                ["Elasticsearch:Uri"] = "http://localhost:9200"
            })
            .Build();

        // Act
        services.AddLenoInfrastructure(config);

        // Assert — IConnectionMultiplexer 应注册为 Singleton
        var registration = services.FirstOrDefault(s => s.ServiceType == typeof(IConnectionMultiplexer));
        registration.Should().NotBeNull();
        registration!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddLenoInfrastructure_RegistersIDistributedLockProviderWithDiFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379,abortConnect=false",
                ["Elasticsearch:Uri"] = "http://localhost:9200"
            })
            .Build();

        // Act
        services.AddLenoInfrastructure(config);

        // Assert — IDistributedLockProvider 应注册（工厂从 DI 解析 IConnectionMultiplexer）
        var registration = services.FirstOrDefault(s => s.ServiceType == typeof(Medallion.Threading.IDistributedLockProvider));
        registration.Should().NotBeNull();
        registration!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddLenoInfrastructure_RegistersIIdempotencyStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379,abortConnect=false",
                ["Elasticsearch:Uri"] = "http://localhost:9200"
            })
            .Build();

        // Act
        services.AddLenoInfrastructure(config);

        // Assert
        var registration = services.FirstOrDefault(s => s.ServiceType == typeof(Leno.Infrastructure.Abstractions.IIdempotencyStore));
        registration.Should().NotBeNull();
        registration!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
