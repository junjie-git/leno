using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Auth;

/// <summary>
/// 网关头认证处理器，从 X-User-Id/X-Role/X-Shop-Id 头构造 ClaimsPrincipal。
/// 仅在后端服务容器内网部署时使用，头由网关 JWT 验签后注入。
/// </summary>
public sealed class GatewayAuthHandler : AuthenticationHandler<GatewayAuthOptions>
{
    public GatewayAuthHandler(IOptionsMonitor<GatewayAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = Request.Headers["X-Role"].FirstOrDefault() ?? string.Empty;
        var shopId = Request.Headers["X-Shop-Id"].FirstOrDefault();

        // 可选：校验 X-Internal-Call 头
        if (Options.RequireInternalCallHeader)
        {
            var internalCall = Request.Headers["X-Internal-Call"].FirstOrDefault();
            if (string.IsNullOrEmpty(internalCall))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing X-Internal-Call header"));
            }
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role)
        };
        if (!string.IsNullOrEmpty(shopId))
        {
            claims.Add(new Claim(JwtTokenGenerator.ShopIdClaimType, shopId));
        }

        var identity = new ClaimsIdentity(claims, "GatewayHeader");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "GatewayHeader");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
