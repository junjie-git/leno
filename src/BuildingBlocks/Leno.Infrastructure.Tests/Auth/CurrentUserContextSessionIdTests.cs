using System.Security.Claims;
using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace Leno.Infrastructure.Tests.Auth;

public class CurrentUserContextSessionIdTests
{
    [Fact]
    public void SessionId_WithJtiClaim_ReturnsClaimValue()
    {
        var sessionId = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, sessionId),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(x => x.HttpContext).Returns(httpContext);

        var ctx = new CurrentUserContext(accessor.Object);

        ctx.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public void SessionId_WithoutJtiClaim_ReturnsNull()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(x => x.HttpContext).Returns(httpContext);

        var ctx = new CurrentUserContext(accessor.Object);

        ctx.SessionId.Should().BeNull();
    }

    [Fact]
    public void SessionId_WhenUnauthenticated_ReturnsNull()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(x => x.HttpContext).Returns(httpContext);

        var ctx = new CurrentUserContext(accessor.Object);

        ctx.SessionId.Should().BeNull();
    }
}
