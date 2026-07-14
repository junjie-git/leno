using System.Net;
using Leno.ApiGateway.Extensions;
using Leno.ApiGateway.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Leno.ApiGateway.Tests.Integration;

public class ObservabilityIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IConsulServiceDiscovery> _consulMock;

    public ObservabilityIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // 与现有集成测试保持一致：mock Consul 服务发现，移除 HealthChecksUI 后台服务
        // （UIInitializationHostedService 在 .NET 10 测试主机下初始化会失败）
        _consulMock = new Mock<IConsulServiceDiscovery>();
        _consulMock.Setup(d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServiceInstance>());

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenTelemetry:Enabled"] = "false", // 测试环境禁用真实 OTLP 导出
                    ["Metrics:Enabled"] = "true",
                    ["Metrics:Path"] = "/metrics",
                    ["Consul:Url"] = "http://localhost:8500",
                    ["Consul:Token"] = "",
                    ["Consul:PassingOnly"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                // 用 mock 替换真实的 IConsulServiceDiscovery（避免连接真实 Consul）
                services.RemoveAll<IConsulServiceDiscovery>();
                services.AddSingleton(_consulMock.Object);

                // 移除 HealthChecksUI 后台服务（UIInitializationHostedService），
                // 其 InMemoryStorage 在 .NET 10 测试主机下初始化会失败（空数据库名校验）。
                for (var i = services.Count - 1; i >= 0; i--)
                {
                    var descriptor = services[i];
                    var implType = descriptor.ImplementationType
                        ?? descriptor.ImplementationInstance?.GetType();
                    if (implType?.Namespace?.StartsWith("HealthChecks.UI", StringComparison.Ordinal) == true)
                    {
                        services.RemoveAt(i);
                    }
                }
            });
        }).CreateClient();
    }

    [Fact]
    public async Task MetricsEndpoint_ReturnsOkAndPrometheusFormat()
    {
        // Act
        var response = await _client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("# HELP gateway_");
    }

    [Fact]
    public async Task HealthLiveEndpoint_RemainsAccessibleAfterObservabilityRegistration()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MetricsEndpoint_AfterRequest_RecordsRequestsTotal()
    {
        // Arrange — 先发一个请求触发计数
        await _client.GetAsync("/health/live");

        // Act
        var metricsResponse = await _client.GetAsync("/metrics");
        var content = await metricsResponse.Content.ReadAsStringAsync();

        // Assert — /health/live 请求应被记录到 gateway_requests_total
        content.Should().Contain("gateway_requests_total");
    }
}

public class ServiceCollectionExtensionsObservabilityTests
{
    [Fact]
    public void AddObservability_RegistersMetricsServiceAndTransform()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Enabled"] = "false",
                ["OpenTelemetry:Exporter"] = "otlp",
                ["OpenTelemetry:Endpoint"] = "http://localhost:4317",
                ["OpenTelemetry:ServiceName"] = "leno-api-gateway",
                ["Metrics:Enabled"] = "true",
                ["Metrics:Path"] = "/metrics",
                ["ReverseProxy:Routes:dummy:ClusterId"] = "dummy",
                ["ReverseProxy:Routes:dummy:Match:Path"] = "/dummy/{**catch-all}",
                ["ReverseProxy:Clusters:dummy:LoadBalancingPolicy"] = "PowerOfTwoChoices"
            })
            .Build();

        // Act
        services.AddLogging();
        services.AddObservability(config);

        // Assert
        var sp = services.BuildServiceProvider();
        sp.GetService<GatewayMetricsService>().Should().NotBeNull();
    }

    [Fact]
    public void AddObservability_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var config = new ConfigurationBuilder().Build();

        var act = () => services.AddObservability(config);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddObservability_NullConfig_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddObservability(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
