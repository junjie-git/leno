using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Infrastructure.Tests.Auth;

public class GatewayAuthHandlerTests
{
    [Fact]
    public async Task HandleAuthenticateAsync_WithValidHeaders_ShouldAuthenticate()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var role = "Seller";
        var shopId = Guid.NewGuid().ToString();

        var handler = CreateHandler(new Dictionary<string, string>
        {
            { "X-User-Id", userId },
            { "X-Role", role },
            { "X-Shop-Id", shopId }
        });

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(JwtRegisteredClaimNames.Sub)!.Value.Should().Be(userId);
        result.Principal!.FindFirst(ClaimTypes.Role)!.Value.Should().Be(role);
        result.Principal!.FindFirst("shop_id")!.Value.Should().Be(shopId);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_MissingUserIdHeader_ShouldNotAuthenticate()
    {
        // Arrange: 缺少 X-User-Id
        var handler = CreateHandler(new Dictionary<string, string>
        {
            { "X-Role", "Seller" }
        });

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_NoHeaders_ShouldNotAuthenticate()
    {
        // Arrange: 无任何头
        var handler = CreateHandler(new Dictionary<string, string>());

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    private static GatewayAuthHandler CreateHandler(Dictionary<string, string> headers)
    {
        var options = new GatewayAuthOptions { HeaderPrefix = "X-" };
        var optionsMonitor = new Mock<IOptionsMonitor<GatewayAuthOptions>>();
        optionsMonitor.Setup(o => o.Get(It.IsAny<string>())).Returns(options);

        var httpContext = new DefaultHttpContext();
        foreach (var kv in headers)
        {
            httpContext.Request.Headers[kv.Key] = kv.Value;
        }

        var handler = new GatewayAuthHandler(
            optionsMonitor.Object,
            new LoggerFactory(),
            UrlEncoder.Default);
        handler.InitializeAsync(new AuthenticationScheme("GatewayHeader", null, typeof(GatewayAuthHandler)), httpContext).GetAwaiter().GetResult();
        return handler;
    }
}
