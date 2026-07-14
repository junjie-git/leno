using System.Net.Http;
using System.Security.Claims;
using Leno.ApiGateway.Transforms;
using Microsoft.AspNetCore.Http;

namespace Leno.ApiGateway.Tests.Transforms;

public class UserContextTransformProviderTests
{
    private static DefaultHttpContext CreateHttpContextWithClaims(params (string Type, string Value)[] claims)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "TestAuth"));
        return httpContext;
    }

    [Fact]
    public void ApplyUserContextHeaders_WithAuthenticatedUser_InjectsAllHeaders()
    {
        // Arrange
        var httpContext = CreateHttpContextWithClaims(
            ("Sub", "12345"),
            ("Role", "Admin"),
            ("shop_id", "shop-001"));
        var proxyRequest = new HttpRequestMessage();

        // Act
        UserContextTransformProvider.ApplyUserContextHeaders(httpContext, proxyRequest);

        // Assert
        proxyRequest.Headers.GetValues(UserContextTransformProvider.XUserId).Should().Contain("12345");
        proxyRequest.Headers.GetValues(UserContextTransformProvider.XRole).Should().Contain("Admin");
        proxyRequest.Headers.GetValues(UserContextTransformProvider.XShopId).Should().Contain("shop-001");
        proxyRequest.Headers.GetValues(UserContextTransformProvider.XInternalCall).Should().Contain("true");
    }

    [Fact]
    public void ApplyUserContextHeaders_WithAnonymousUser_OnlyInjectsInternalCall()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        var proxyRequest = new HttpRequestMessage();

        // Act
        UserContextTransformProvider.ApplyUserContextHeaders(httpContext, proxyRequest);

        // Assert
        proxyRequest.Headers.Contains(UserContextTransformProvider.XUserId).Should().BeFalse();
        proxyRequest.Headers.Contains(UserContextTransformProvider.XRole).Should().BeFalse();
        proxyRequest.Headers.Contains(UserContextTransformProvider.XShopId).Should().BeFalse();
        proxyRequest.Headers.GetValues(UserContextTransformProvider.XInternalCall).Should().Contain("true");
    }

    [Fact]
    public void ApplyUserContextHeaders_WithPartialClaims_InjectsOnlyPresentHeaders()
    {
        // Arrange
        var httpContext = CreateHttpContextWithClaims(("Sub", "999"));
        var proxyRequest = new HttpRequestMessage();

        // Act
        UserContextTransformProvider.ApplyUserContextHeaders(httpContext, proxyRequest);

        // Assert
        proxyRequest.Headers.GetValues(UserContextTransformProvider.XUserId).Should().Contain("999");
        proxyRequest.Headers.Contains(UserContextTransformProvider.XRole).Should().BeFalse();
        proxyRequest.Headers.Contains(UserContextTransformProvider.XShopId).Should().BeFalse();
        proxyRequest.Headers.GetValues(UserContextTransformProvider.XInternalCall).Should().Contain("true");
    }

    [Fact]
    public void RemoveInternalHeaders_RemovesXInternalCallFromResponse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Headers[UserContextTransformProvider.XInternalCall] = "true";

        // Act
        UserContextTransformProvider.RemoveInternalHeaders(httpContext);

        // Assert
        httpContext.Response.Headers.ContainsKey(UserContextTransformProvider.XInternalCall).Should().BeFalse();
    }

    [Fact]
    public void RemoveInternalHeaders_WhenHeaderAbsent_DoesNotThrow()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act
        var act = () => UserContextTransformProvider.RemoveInternalHeaders(httpContext);

        // Assert
        act.Should().NotThrow();
    }
}
