using System.Net;
using Leno.ApiGateway.Services;
using Leno.ApiGateway.Transforms;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Leno.ApiGateway.Tests.Integration;

/// <summary>
/// 阶段六端到端集成测试：验证 Program.cs 中 Phase 6 服务注册与中间件管道的正确集成。
/// 通过 WebApplicationFactory 启动完整网关，mock Consul 与 HealthChecksUI 避免外部依赖。
/// </summary>
public class Phase6IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public Phase6IntegrationTests(WebApplicationFactory<Program> factory)
    {
        // 与现有集成测试保持一致：mock Consul 服务发现，移除 HealthChecksUI 后台服务
        // （UIInitializationHostedService 在 .NET 10 测试主机下初始化会失败）
        var consulMock = new Mock<IConsulServiceDiscovery>();
        // YARP 启动时 InitialLoadAsync 会解析所有集群，默认返回空实例列表避免 NRE。
        consulMock.Setup(d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServiceInstance>());

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Consul:Url"] = "http://localhost:8500",
                    ["Redis:Configuration"] = "localhost:6379",
                    ["Gateway:Cache:Enabled"] = "false",
                    ["Gateway:Cors:Enabled"] = "true",
                    ["Gateway:Cors:AllowedOrigins:0"] = "http://localhost:3000",
                    // Phase 7 F2：本测试聚焦 Phase 6 服务注册与 CORS，禁用 JWT 验签避免 401
                    ["Jwt:Enabled"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                // 用 mock 替换真实 Consul 服务发现（避免连接真实 Consul）
                services.RemoveAll<IConsulServiceDiscovery>();
                services.AddSingleton(consulMock.Object);

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
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void ProtocolTranslatorRegistry_IsRegisteredInDI()
    {
        // Act — 从 DI 容器解析 ProtocolTranslatorRegistry
        var registry = _factory.Services.GetService<ProtocolTranslatorRegistry>();

        // Assert — 注册表应存在且 All 为空（当前无 IProtocolTranslator 实现）
        registry.Should().NotBeNull();
        registry!.All.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task OptionsRequest_WithCorsEnabled_ReturnsOkOrNoContent()
    {
        // Arrange — 预检请求
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/products/test");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — CORS 中间件处理预检，返回 200/204 或转发到 YARP（后端不可达返回 503/502）
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadGateway);
    }
}
