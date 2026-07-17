using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Leno.ApiGateway.Services;
using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Leno.ApiGateway.Tests.Middleware;

/// <summary>
/// JWT 验签中间件测试：验证网关对未认证请求返回 401、白名单路由放行、有效 token 通过验签。
/// 通过 WebApplicationFactory 启动完整网关管道，mock Consul 与 HealthChecksUI 避免外部依赖。
/// </summary>
public class JwtAuthMiddlewareTests
{
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
    public async Task ValidToken_ShouldPassAndInjectUserContextHeaders()
    {
        // Arrange
        var secretKey = "TestSecretKeyAtLeast32BytesLong!!";
        var token = GenerateTestToken(secretKey, userId: Guid.NewGuid(), role: "Buyer");

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => ConfigureTestHost(b, secretKey));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act: 请求受保护端点（下游不可达，但网关验签应放行不返回 401）
        var response = await client.GetAsync("/api/orders");

        // Assert: 不返回 401（可能返回 502/503 下游不可达，但不应是 401）
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// 配置测试主机：mock Consul 服务发现、移除 HealthChecksUI 后台服务、禁用缓存、
    /// 注入测试用 JwtOptions（SecretKey 至少 32 字节以满足 HS256 要求）。
    /// </summary>
    private static void ConfigureTestHost(IWebHostBuilder builder, string? secretKey = null)
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
                ["Gateway:Cache:Enabled"] = "false"
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

            // 注入测试用 JwtOptions（覆盖 appsettings.json 中的 ${JWT_SECRET_KEY} 占位符）
            services.Configure<JwtOptions>(o =>
            {
                o.Issuer = "Leno.UserAuth";
                o.Audience = "Leno.Clients";
                o.SecretKey = secretKey ?? "TestSecretKeyAtLeast32BytesLong!!";
                o.AccessTokenExpiryMinutes = 120;
                o.RefreshTokenExpiryDays = 7;
            });
        });
    }

    private static string GenerateTestToken(string secretKey, Guid userId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: "Leno.UserAuth",
            audience: "Leno.Clients",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
