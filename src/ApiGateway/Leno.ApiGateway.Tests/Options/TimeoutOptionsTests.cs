using Leno.ApiGateway.Options;
using Microsoft.Extensions.Configuration;

namespace Leno.ApiGateway.Tests.Options;

public class TimeoutOptionsTests
{
    private static TimeoutOptions BindFromDictionary(IDictionary<string, string?> data)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
        var opts = new TimeoutOptions();
        config.GetSection("Timeout").Bind(opts);
        return opts;
    }

    [Fact]
    public void Bind_FromConfiguration_PopulatesAllPolicies()
    {
        // Arrange
        var data = new Dictionary<string, string?>
        {
            ["Timeout:DefaultPolicy"] = "leno-default",
            ["Timeout:Policies:leno-default:RouteType"] = "default",
            ["Timeout:Policies:leno-default:RequestTimeout"] = "00:00:30",
            ["Timeout:Policies:leno-default:ConnectTimeout"] = "00:00:05",
            ["Timeout:Policies:leno-default:ReadTimeout"] = "00:00:30",
            ["Timeout:Policies:seckill:RouteType"] = "seckill",
            ["Timeout:Policies:seckill:RequestTimeout"] = "00:00:05",
            ["Timeout:Policies:seckill:ConnectTimeout"] = "00:00:02",
            ["Timeout:Policies:seckill:ReadTimeout"] = "00:00:05",
            ["Timeout:Policies:upload:RouteType"] = "upload",
            ["Timeout:Policies:upload:RequestTimeout"] = "00:02:00",
            ["Timeout:Policies:upload:ConnectTimeout"] = "00:00:10",
            ["Timeout:Policies:upload:ReadTimeout"] = "00:02:00",
            ["Timeout:Policies:internal:RouteType"] = "internal",
            ["Timeout:Policies:internal:RequestTimeout"] = "00:00:15",
            ["Timeout:Policies:internal:ConnectTimeout"] = "00:00:03",
            ["Timeout:Policies:internal:ReadTimeout"] = "00:00:15"
        };

        // Act
        var opts = BindFromDictionary(data);

        // Assert
        opts.DefaultPolicy.Should().Be("leno-default");
        opts.Policies.Should().HaveCount(4);
        opts.Policies["leno-default"].RequestTimeout.Should().Be(TimeSpan.FromSeconds(30));
        opts.Policies["leno-default"].ConnectTimeout.Should().Be(TimeSpan.FromSeconds(5));
        opts.Policies["seckill"].RequestTimeout.Should().Be(TimeSpan.FromSeconds(5));
        opts.Policies["seckill"].ConnectTimeout.Should().Be(TimeSpan.FromSeconds(2));
        opts.Policies["upload"].RequestTimeout.Should().Be(TimeSpan.FromSeconds(120));
        opts.Policies["upload"].ConnectTimeout.Should().Be(TimeSpan.FromSeconds(10));
        opts.Policies["internal"].RequestTimeout.Should().Be(TimeSpan.FromSeconds(15));
        opts.Policies["internal"].ConnectTimeout.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        var opts = new TimeoutOptions();
        opts.DefaultPolicy.Should().Be("leno-default");
        opts.Policies.Should().BeEmpty();

        var policy = new TimeoutPolicyOptions();
        policy.RequestTimeout.Should().Be(TimeSpan.FromSeconds(30));
        policy.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(5));
        policy.ReadTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void RouteTypeConstants_MatchExpectedValues()
    {
        TimeoutRouteTypes.Default.Should().Be("default");
        TimeoutRouteTypes.Seckill.Should().Be("seckill");
        TimeoutRouteTypes.Upload.Should().Be("upload");
        TimeoutRouteTypes.Internal.Should().Be("internal");
    }

    [Fact]
    public void Bind_WithEmptyPolicies_DoesNotThrow()
    {
        var opts = BindFromDictionary(new Dictionary<string, string?>());
        opts.Policies.Should().BeEmpty();
        opts.DefaultPolicy.Should().Be("leno-default");
    }
}
