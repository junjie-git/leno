using Leno.ApiGateway.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Extensions;

public class RedisExtensionsTests
{
    [Fact]
    public void AddGatewayRedis_RegistersConnectionMultiplexerAndDatabase()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379"
            })
            .Build();

        // Act
        services.AddGatewayRedis(config);
        var sp = services.BuildServiceProvider();

        // Assert — 单例 IConnectionMultiplexer 注册（实际连接不发生，AbortOnConnectFail=false）
        services.Should().Contain(s => s.ServiceType == typeof(IConnectionMultiplexer));
        services.Should().Contain(s => s.ServiceType == typeof(IDatabase));
    }

    [Fact]
    public void AddGatewayRedis_FallsBackToConnectionStringKey()
    {
        // Arrange — 不提供 Redis:Configuration，使用 ConnectionStrings:Redis
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "redis-host:6380"
            })
            .Build();

        // Act
        services.AddGatewayRedis(config);

        // Assert — 不抛异常即说明回退成功
        var sp = services.BuildServiceProvider();
        var multiplexer = sp.GetService<IConnectionMultiplexer>();
        multiplexer.Should().NotBeNull();
    }

    [Fact]
    public void AddGatewayRedis_UsesDefaultWhenConfigMissing()
    {
        // Arrange — 不提供任何 Redis 配置
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        // Act
        services.AddGatewayRedis(config);

        // Assert — 使用默认 localhost:6379
        var sp = services.BuildServiceProvider();
        sp.GetService<IConnectionMultiplexer>().Should().NotBeNull();
    }

    [Fact]
    public void AddGatewayRedis_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var config = new ConfigurationBuilder().Build();

        var act = () => services.AddGatewayRedis(config);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGatewayRedis_NullConfig_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddGatewayRedis(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
