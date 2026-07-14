using Leno.ApiGateway.Extensions;
using Leno.ApiGateway.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Extensions;

public class RateLimiterExtensionsTests
{
    private static IConfiguration CreateConfig(bool useRedis = true) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:UseRedisDistributed"] = useRedis.ToString(),
                ["RateLimit:RedisKeyPrefix"] = "leno:rl:",
                ["RateLimit:Global:TokenLimit"] = "5000",
                ["RateLimit:Routes:default:PermitLimit"] = "200",
                ["RateLimit:Routes:default:Window"] = "00:00:01",
                ["RateLimit:Routes:seckill:PermitLimit"] = "50",
                ["RateLimit:Routes:seckill:Window"] = "00:00:01",
                ["RateLimit:User:PermitLimit"] = "100",
                ["RateLimit:User:Window"] = "00:01:00"
            })
            .Build();

    [Fact]
    public void AddGatewayRateLimiter_RegistersRateLimiterMiddleware()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateConfig();

        // Act
        services.AddGatewayRateLimiter(config);

        // Assert — AddRateLimiter 通过 Configure 注册了 IConfigureOptions<RateLimiterOptions>
        // （IOptions<T> 本身由 AddOptions() 以开放泛型注册，无法在 ServiceDescriptor 中按封闭泛型匹配）
        services.Should().Contain(s => s.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>));
    }

    [Fact]
    public void AddGatewayRateLimiter_BindsRateLimitOptionsFromConfig()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateConfig();
        services.AddGatewayRateLimiter(config);

        // Act
        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<RateLimitOptions>>().Value;

        // Assert
        opts.Global.TokenLimit.Should().Be(5000);
        opts.Routes["default"].PermitLimit.Should().Be(200);
        opts.Routes["seckill"].PermitLimit.Should().Be(50);
        opts.User.PermitLimit.Should().Be(100);
        opts.UseRedisDistributed.Should().BeTrue();
    }

    [Fact]
    public void AddGatewayRateLimiter_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var config = CreateConfig();

        var act = () => services.AddGatewayRateLimiter(config);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGatewayRateLimiter_NullConfig_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddGatewayRateLimiter(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGatewayRateLimiter_DoesNotRequireRedisAtRegistrationTime()
    {
        // Arrange — 注册时不依赖 IDatabase（解析分区时才需要）
        var services = new ServiceCollection();
        var config = CreateConfig(useRedis: true);

        // Act
        services.AddGatewayRateLimiter(config);

        // Assert — 应能成功构建 ServiceProvider（不报缺失 IDatabase）
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IOptions<RateLimiterOptions>>().Should().NotBeNull();
    }

    [Fact]
    public void Policies_ConstantsMatchExpectedNames()
    {
        RateLimiterExtensions.Policies.Global.Should().Be("global");
        RateLimiterExtensions.Policies.Default.Should().Be("default");
        RateLimiterExtensions.Policies.Seckill.Should().Be("seckill");
        RateLimiterExtensions.Policies.PerUser.Should().Be("per-user");
    }
}
