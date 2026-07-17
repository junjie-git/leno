using System.Security.Claims;
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Services;
using Leno.ApiGateway.Tests.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Prometheus;

namespace Leno.ApiGateway.Tests.Middleware;

/// <summary>
/// JWT 黑名单中间件测试。
/// 因 <see cref="GatewayMetricsService"/> 为 sealed 且 <see cref="GatewayMetricsService.RecordBlacklistHit"/> 非虚方法，
/// Moq 无法拦截；改用真实实例 + 隔离 <see cref="CollectorRegistry"/>，通过读取 gateway_blacklist_hits 计数器值断言。
/// </summary>
public class JwtBlacklistMiddlewareTests
{
    private readonly Mock<IJwtBlacklistService> _blacklistMock = new();
    private readonly CollectorRegistry _registry = new();
    private readonly GatewayMetricsService _metrics;

    public JwtBlacklistMiddlewareTests()
    {
        _metrics = new GatewayMetricsService(_registry);
    }

    [Fact]
    public async Task Request_WithBlacklistedJti_ShouldReturn401AndRecordHit()
    {
        // Arrange: jti 在黑名单中
        var jti = Guid.NewGuid().ToString();
        _blacklistMock.Setup(b => b.IsRevokedAsync(jti, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        }, "Bearer"));

        var middleware = new JwtBlacklistMiddleware(_ => Task.CompletedTask, _blacklistMock.Object, _metrics);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _registry.GetSingleValue("gateway_blacklist_hits").Should().Be(1);
    }

    [Fact]
    public async Task Request_WithValidJti_ShouldPassThrough()
    {
        // Arrange: jti 不在黑名单
        var jti = Guid.NewGuid().ToString();
        _blacklistMock.Setup(b => b.IsRevokedAsync(jti, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        }, "Bearer"));

        var nextCalled = false;
        var middleware = new JwtBlacklistMiddleware(_ => { nextCalled = true; return Task.CompletedTask; },
            _blacklistMock.Object, _metrics);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _registry.GetSingleValue("gateway_blacklist_hits").Should().Be(0);
    }
}
