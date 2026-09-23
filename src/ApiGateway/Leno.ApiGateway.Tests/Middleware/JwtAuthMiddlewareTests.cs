using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.ApiGateway.Services;
using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.ApiGateway.Tests.Middleware;

/// <summary>
/// JWT 验签中间件测试：验证网关对未认证请求返回 401、白名单路由放行、已认证请求放行并注入用户上下文头。
/// 通过 WebApplicationFactory 启动完整网关管道，mock Consul 与 HealthChecksUI 避免外部依赖。
/// <para>
/// 双轨下线 A6（2026-09-23）：网关验签已切 RS256/JWKS（原 HS256 共享密钥用例随之删除）。
/// 测试用 <see cref="GatewayTestAuthHandler"/> 替换 JwtBearer 方案 —— 携带 Authorization 头即视为已认证，
/// 无头则匿名（走 401/白名单路径），与签名算法无关。
/// </para>
/// </summary>
public class JwtAuthMiddlewareTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task UnauthenticatedRequest_ToProtectedEndpoint_ShouldReturn401()
    {
        // Arrange
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => ConfigureTestHost(b));

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert: 受保护端点无 token 返回 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WhitelistedEndpoint_NoToken_ShouldReturn200()
    {
        // Arrange
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => ConfigureTestHost(b));

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert: 白名单路由无 token 返回 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthenticatedRequest_ShouldPassAndInjectUserContextHeaders()
    {
        // Arrange
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => ConfigureTestHost(b));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User", UserId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Buyer");

        // Act: 请求受保护端点（下游不可达，但网关验签应放行不返回 401）
        var response = await client.GetAsync("/api/orders");

        // Assert: 不返回 401（可能返回 502/503 下游不可达，但不应是 401）
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// 配置测试主机：mock Consul 服务发现、移除 HealthChecksUI 后台服务、禁用缓存、
    /// 以 <see cref="GatewayTestAuthHandler"/>（算法无关）替换默认 JwtBearer 方案。
    /// </summary>
    private static void ConfigureTestHost(IWebHostBuilder builder)
    {
        var consulMock = new Mock<IConsulServiceDiscovery>();
        // YARP 启动时 InitialLoadAsync 会解析所有集群，默认返回空实例列表避免 NRE。
        consulMock.Setup(d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServiceInstance>());

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Consul:Url"] = "http://localhost:8500",
                ["Consul:Token"] = "",
                ["Consul:PassingOnly"] = "true",
                // Phase 6 集成后 CacheMiddleware 会访问 Redis，测试环境禁用缓存避免 500
                ["Gateway:Cache:Enabled"] = "false",
                ["Jwt:DiscoveryUrl"] = "http://localhost:5162/.well-known/openid-configuration"
            });
        });

        builder.ConfigureServices(services =>
        {
            // 用 mock 替换真实的 IConsulServiceDiscovery（避免连接真实 Consul）
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

            // 以 Test 方案替换 JwtBearer（算法无关）：带 Authorization 头即认证成功并注入测试 claims
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = GatewayTestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = GatewayTestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, GatewayTestAuthHandler>(
                    GatewayTestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <summary>
    /// 算法无关的测试鉴权处理器：携带 Authorization: Bearer 头即认证成功，
    /// 用户/角色取自 X-Test-User / X-Test-Role 头（默认新 Guid 与 Buyer）。
    /// </summary>
    private sealed class GatewayTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";

        public GatewayTestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var auth) ||
                !auth.ToString().StartsWith("Bearer ", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            Request.Headers.TryGetValue("X-Test-User", out var user);
            Request.Headers.TryGetValue("X-Test-Role", out var role);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
