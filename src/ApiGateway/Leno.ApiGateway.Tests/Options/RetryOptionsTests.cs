using Leno.ApiGateway.Options;
using Microsoft.Extensions.Configuration;

namespace Leno.ApiGateway.Tests.Options;

public class RetryOptionsTests
{
    private static RetryOptions BindFromDictionary(IDictionary<string, string?> data)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
        var opts = new RetryOptions();
        config.GetSection("Retry").Bind(opts);
        return opts;
    }

    [Fact]
    public void Bind_FromConfiguration_PopulatesAllFields()
    {
        // Arrange
        var data = new Dictionary<string, string?>
        {
            ["Retry:MaxRetries"] = "2",
            ["Retry:Backoff"] = "Exponential",
            ["Retry:MinBackoff"] = "00:00:00.500",
            ["Retry:MaxBackoff"] = "00:00:01",
            ["Retry:RetryableStatusCodes:0"] = "503",
            ["Retry:RetryableStatusCodes:1"] = "504",
            ["Retry:IdempotentMethodsOnly"] = "true"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
        var opts = new RetryOptions();
        // .NET 10 ConfigurationBinder 会把配置项追加到数组属性的现有值上，
        // 先清空默认数组，使绑定结果精确反映 appsettings 中的配置值。
        opts.RetryableStatusCodes = Array.Empty<int>();

        // Act
        config.GetSection("Retry").Bind(opts);

        // Assert
        opts.MaxRetries.Should().Be(2);
        opts.Backoff.Should().Be("Exponential");
        opts.MinBackoff.Should().Be(TimeSpan.FromMilliseconds(500));
        opts.MaxBackoff.Should().Be(TimeSpan.FromSeconds(1));
        opts.RetryableStatusCodes.Should().Equal(503, 504);
        opts.IdempotentMethodsOnly.Should().BeTrue();
    }

    [Fact]
    public void Defaults_MatchSpecRequirements()
    {
        // Spec 5.3: 最多 2 次重试，指数退避 500ms→1000ms，仅幂等方法，重试条件 503
        var opts = new RetryOptions();

        opts.MaxRetries.Should().Be(2);
        opts.Backoff.Should().Be("Exponential");
        opts.MinBackoff.Should().Be(TimeSpan.FromMilliseconds(500));
        opts.MaxBackoff.Should().Be(TimeSpan.FromSeconds(1));
        opts.RetryableStatusCodes.Should().Equal(503);
        opts.IdempotentMethodsOnly.Should().BeTrue();
    }

    [Fact]
    public void RetryEnabledClusters_ContainsAllElevenServices()
    {
        RetryRouteTypes.RetryEnabledClusters.Should().HaveCount(11);
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("user-auth");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("product");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("cart");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("order");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("promotion");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("payment");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("points");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("review-aftersales");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("seller-shop");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("notification");
        RetryRouteTypes.RetryEnabledClusters.Should().Contain("system-admin");
    }

    [Fact]
    public void Bind_WithMissingFields_UsesDefaults()
    {
        var opts = BindFromDictionary(new Dictionary<string, string?>());
        opts.MaxRetries.Should().Be(2);
        opts.Backoff.Should().Be("Exponential");
        opts.RetryableStatusCodes.Should().Equal(503);
    }
}
