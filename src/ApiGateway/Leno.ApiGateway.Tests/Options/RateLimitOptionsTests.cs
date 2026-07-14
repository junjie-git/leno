using Leno.ApiGateway.Options;
using Microsoft.Extensions.Configuration;

namespace Leno.ApiGateway.Tests.Options;

public class RateLimitOptionsTests
{
    private static RateLimitOptions BindFromDictionary(IDictionary<string, string?> data)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
        var opts = new RateLimitOptions();
        config.GetSection("RateLimit").Bind(opts);
        return opts;
    }

    [Fact]
    public void Bind_FromConfiguration_PopulatesAllSections()
    {
        // Arrange
        var data = new Dictionary<string, string?>
        {
            ["RateLimit:UseRedisDistributed"] = "true",
            ["RateLimit:RedisKeyPrefix"] = "leno:rl:",
            ["RateLimit:Global:TokenLimit"] = "5000",
            ["RateLimit:Global:TokensPerPeriod"] = "5000",
            ["RateLimit:Global:ReplenishmentPeriod"] = "00:00:01",
            ["RateLimit:Routes:default:PermitLimit"] = "200",
            ["RateLimit:Routes:default:Window"] = "00:00:01",
            ["RateLimit:Routes:default:SegmentsPerWindow"] = "4",
            ["RateLimit:Routes:seckill:PermitLimit"] = "50",
            ["RateLimit:Routes:seckill:Window"] = "00:00:01",
            ["RateLimit:Routes:seckill:SegmentsPerWindow"] = "4",
            ["RateLimit:User:PermitLimit"] = "100",
            ["RateLimit:User:Window"] = "00:01:00",
            ["RateLimit:User:SegmentsPerWindow"] = "6"
        };

        // Act
        var opts = BindFromDictionary(data);

        // Assert
        opts.UseRedisDistributed.Should().BeTrue();
        opts.RedisKeyPrefix.Should().Be("leno:rl:");
        opts.Global.TokenLimit.Should().Be(5000);
        opts.Global.TokensPerPeriod.Should().Be(5000);
        opts.Global.ReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(1));
        opts.Routes.Should().ContainKey("default");
        opts.Routes["default"].PermitLimit.Should().Be(200);
        opts.Routes["default"].Window.Should().Be(TimeSpan.FromSeconds(1));
        opts.Routes["default"].SegmentsPerWindow.Should().Be(4);
        opts.Routes["seckill"].PermitLimit.Should().Be(50);
        opts.User.PermitLimit.Should().Be(100);
        opts.User.Window.Should().Be(TimeSpan.FromMinutes(1));
        opts.User.SegmentsPerWindow.Should().Be(6);
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        var opts = new RateLimitOptions();

        opts.Global.TokenLimit.Should().Be(5000);
        opts.Global.TokensPerPeriod.Should().Be(5000);
        opts.Global.ReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(1));
        opts.User.PermitLimit.Should().Be(100);
        opts.User.Window.Should().Be(TimeSpan.FromMinutes(1));
        opts.UseRedisDistributed.Should().BeTrue();
        opts.RedisKeyPrefix.Should().Be("leno:ratelimit:");
    }

    [Fact]
    public void Bind_WithMissingSections_UsesDefaults()
    {
        // Arrange — 不提供任何 RateLimit 配置
        var config = new ConfigurationBuilder().Build();

        // Act
        var opts = new RateLimitOptions();
        config.Bind(opts);

        // Assert — 应使用代码默认值
        opts.Global.TokenLimit.Should().Be(5000);
        opts.User.PermitLimit.Should().Be(100);
        opts.Routes.Should().BeEmpty();
    }

    [Fact]
    public void Bind_ParsesTimeSpanInIsoFormat()
    {
        var data = new Dictionary<string, string?>
        {
            ["RateLimit:Global:ReplenishmentPeriod"] = "00:00:02",
            ["RateLimit:User:Window"] = "00:02:30"
        };

        var opts = BindFromDictionary(data);

        opts.Global.ReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(2));
        opts.User.Window.Should().Be(TimeSpan.FromSeconds(150));
    }
}
