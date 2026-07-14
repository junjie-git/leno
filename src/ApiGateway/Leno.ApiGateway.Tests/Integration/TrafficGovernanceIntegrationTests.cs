using System.Net;
using Leno.ApiGateway.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Leno.ApiGateway.Tests.Integration;

/// <summary>
/// Phase 4 流量治理端到端集成测试。
/// 通过 WebApplicationFactory 启动完整网关管道，验证限流/降级/超时中间件链路。
/// </summary>
public class TrafficGovernanceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IConsulServiceDiscovery> _consulMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDbMock;

    public TrafficGovernanceIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _consulMock = new Mock<IConsulServiceDiscovery>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisDbMock = new Mock<IDatabase>();

        // YARP 启动时 InitialLoadAsync 会解析所有集群，默认返回空实例列表避免 NRE。
        _consulMock.Setup(d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServiceInstance>());

        // Redis 默认返回 1（允许通过），具体测试中可覆写。
        // 注意：ASP.NET Core RateLimiter 中间件会调用 RateLimiter.AttemptAcquire（同步路径），
        // 进而在 RedisSlidingWindowRateLimiter.AttemptAcquireCore 中调用同步的 IDatabase.ScriptEvaluate，
        // 而非异步 ScriptEvaluateAsync。因此必须同时设置同步与异步两个 mock，否则同步路径会返回
        // 默认 null 并触发 NRE 或 fail-open 行为。
        _redisDbMock.Setup(d => d.ScriptEvaluate(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Returns(RedisResult.Create((RedisValue)1, ResultType.Integer));
        _redisDbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1, ResultType.Integer));
        _redisMock.SetupGet(m => m.IsConnected).Returns(true);
        _redisMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDbMock.Object);

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Consul:Url"] = "http://localhost:8500",
                    ["Redis:Configuration"] = "localhost:6379",
                    ["RateLimit:UseRedisDistributed"] = "true",
                    ["RateLimit:Global:TokenLimit"] = "5000",
                    ["RateLimit:Routes:leno-default:PermitLimit"] = "200",
                    ["RateLimit:Routes:leno-default:Window"] = "00:00:01",
                    ["RateLimit:Routes:seckill:PermitLimit"] = "50",
                    ["RateLimit:Routes:seckill:Window"] = "00:00:01",
                    ["RateLimit:User:PermitLimit"] = "100",
                    ["RateLimit:User:Window"] = "00:01:00"
                });
            });

            builder.ConfigureServices(services =>
            {
                // 用 mock 替换真实 Consul 服务发现
                services.RemoveAll<IConsulServiceDiscovery>();
                services.AddSingleton(_consulMock.Object);

                // 用 mock 替换真实 Redis 连接（避免测试依赖真实 Redis）
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton(_redisMock.Object);

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
    public async Task HealthLive_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_ShouldNotBeRewrittenByFallbackMiddleware()
    {
        // Arrange — /health/ready 在 Consul 不可用时返回 503，但 FallbackResponseMiddleware 应跳过健康端点
        var response = await _client.GetAsync("/health/ready");

        // Assert — 503 但响应体不是降级 JSON
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            body.Should().NotContain("服务暂时不可用，请稍后重试");
        }
    }

    [Fact]
    public async Task Proxy_WhenBackendReturns503_RewritesAsFallbackJson()
    {
        // Arrange — 模拟 Consul 返回一个不存在的实例（YARP 转发将失败返回 502/503）
        _consulMock.Setup(d => d.GetHealthyInstancesAsync("leno-product-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceInstance>
            {
                new("test-1", "localhost", 5150, Array.Empty<string>())
            });

        // Act — 发送请求到 product 路由
        var response = await _client.GetAsync("/api/products/test-id");

        // Assert — 网关转发失败（502/503），FallbackResponseMiddleware 应改写 503 为降级 JSON
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("\"code\":503");
            body.Should().Contain("服务暂时不可用，请稍后重试");
        }
        else
        {
            // 502 时不改写（FallbackResponseMiddleware 只处理 503）
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadGateway);
        }
    }

    [Fact]
    public async Task Proxy_WhenNoHealthyInstances_Returns503WithFallbackJson()
    {
        // Arrange — Consul 返回空实例列表
        _consulMock.Setup(d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServiceInstance>());

        // Act
        var response = await _client.GetAsync("/api/cart/test");

        // Assert — YARP 返回 503（无可用 destination），FallbackResponse 应改写为降级 JSON
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"code\":503");
        body.Should().Contain("服务暂时不可用，请稍后重试");
    }

    [Fact]
    public async Task Proxy_WithSeckillRoute_AppliesSeckillRateLimiterPolicy()
    {
        // Arrange — 模拟 Consul 返回健康实例
        _consulMock.Setup(d => d.GetHealthyInstancesAsync("leno-promotion-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceInstance>
            {
                new("promo-1", "localhost", 5152, Array.Empty<string>())
            });

        // Act — 发送到秒杀路由，应触发 "seckill" 策略
        var response = await _client.GetAsync("/api/seckill/123");

        // Assert — Redis 被调用且 Key 包含 "seckill" 策略名。
        // RateLimiter 中间件走同步路径（AttemptAcquire → ScriptEvaluate），故校验同步方法。
        _redisDbMock.Verify(d => d.ScriptEvaluate(
            It.IsAny<string>(),
            It.Is<RedisKey[]>(keys => keys.Length > 0 && keys[0].ToString().Contains("seckill")),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()), Times.AtLeastOnce);

        // 响应可能是 502（后端不可达）或 503，但管道不应崩溃
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Proxy_WhenRedisDenies_Returns429TooManyRequests()
    {
        // Arrange — Redis 返回 0（拒绝），模拟限流触发。
        // 同步与异步两条路径均需设置，因 RateLimiter 中间件走同步路径。
        _redisDbMock.Setup(d => d.ScriptEvaluate(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Returns(RedisResult.Create((RedisValue)0, ResultType.Integer));
        _redisDbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)0, ResultType.Integer));

        _consulMock.Setup(d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceInstance>
            {
                new("test-1", "localhost", 5150, Array.Empty<string>())
            });

        // Act
        var response = await _client.GetAsync("/api/products/test-id");

        // Assert — Redis 拒绝应导致 429
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Proxy_WhenRedisThrows_FailsOpenAndForwardsRequest()
    {
        // Arrange — Redis 异常，限流器 fail-open 放行。
        // 同步与异步两条路径均需抛异常以验证 fail-open 行为。
        _redisDbMock.Setup(d => d.ScriptEvaluate(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));
        _redisDbMock.Setup(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection refused"));

        _consulMock.Setup(d => d.GetHealthyInstancesAsync("leno-product-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceInstance>
            {
                new("test-1", "localhost", 5150, Array.Empty<string>())
            });

        // Act
        var response = await _client.GetAsync("/api/products/test-id");

        // Assert — 不应因 Redis 故障返回 429；转发尝试应进行（可能 502 但不是 429）
        response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }
}
