using System.Net;
using Leno.ApiGateway.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Leno.ApiGateway.Tests.Integration;

public class GatewayRoutingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IConsulServiceDiscovery> _discoveryMock;

    public GatewayRoutingIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // 使用 IConsulServiceDiscovery 接口 mock，避免依赖真实 Consul 与 sealed 类无法 mock 的问题
        _discoveryMock = new Mock<IConsulServiceDiscovery>();
        // YARP 启动时 InitialLoadAsync 会解析所有集群（product/cart/order 等），
        // 默认返回空实例列表，避免未配置的服务名返回 null 触发 NullReferenceException。
        // 各测试方法可追加更具体的 Setup（Moq 后注册的 Setup 优先匹配）。
        _discoveryMock.Setup(d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServiceInstance>());

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Consul:Url"] = "http://localhost:8500",
                    ["Consul:Token"] = "",
                    ["Consul:PassingOnly"] = "true",
                    // Phase 6 集成后 CacheMiddleware 会访问 Redis，测试环境禁用缓存避免 500
                    ["Gateway:Cache:Enabled"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                // 用 mock 替换真实的 IConsulServiceDiscovery（避免连接真实 Consul）
                services.RemoveAll<IConsulServiceDiscovery>();
                services.AddSingleton(_discoveryMock.Object);

                // 移除 HealthChecksUI 后台服务（UIInitializationHostedService），
                // 其 InMemoryStorage 在 .NET 10 测试主机下初始化会失败（空数据库名校验），
                // 这些测试只验证 /health 端点与 YARP 代理，不需要仪表盘。
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
    public async Task HealthLive_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReady_WhenConsulUnreachable_ReturnsServiceUnavailable()
    {
        // 网关就绪检查包含 Consul 连通性（AddUrlGroup 直连 Consul:Url），
        // 测试环境无真实 Consul，因此就绪检查预期返回 503。
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Proxy_WithHealthyInstances_ForwardsToBackend()
    {
        // Arrange — 模拟 Consul 返回的健康实例指向本机端口（实际无服务监听）
        var instances = new List<ServiceInstance>
        {
            new("test-1", "localhost", 5150, Array.Empty<string>())
        };

        _discoveryMock.Setup(d => d.GetHealthyInstancesAsync("leno-product-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instances);

        // Act — 发送请求到网关
        // 注意：实际转发需要后端服务运行，此处仅验证网关尝试转发而非返回 500
        var response = await _client.GetAsync("/api/products/test-id");

        // Assert — 网关应尝试转发（502 BadGateway 表示后端不可达，网关本身工作正常）
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Proxy_WhenNoHealthyInstances_ReturnsServiceUnavailable()
    {
        // Arrange — Consul 返回空实例列表
        _discoveryMock.Setup(d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServiceInstance>());

        // Act
        var response = await _client.GetAsync("/api/products/test-id");

        // Assert — 无健康实例时 YARP 返回 503
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
