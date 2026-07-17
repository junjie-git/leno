using Leno.ApiGateway.Services;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Leno.ApiGateway.Middleware;

/// <summary>
/// JWT 黑名单拦截中间件，紧随 UseAuthentication 之后。
/// 命中黑名单返回 401 并递增 gateway_blacklist_hits 计数器。
/// </summary>
public sealed class JwtBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IJwtBlacklistService _blacklistService;
    private readonly GatewayMetricsService _metrics;

    public JwtBlacklistMiddleware(
        RequestDelegate next,
        IJwtBlacklistService blacklistService,
        GatewayMetricsService metrics)
    {
        _next = next;
        _blacklistService = blacklistService;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 仅对已认证请求检查黑名单
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var jti = context.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                if (await _blacklistService.IsRevokedAsync(jti, context.RequestAborted))
                {
                    _metrics.RecordBlacklistHit();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { code = 401, message = "Token 已被吊销" });
                    return;
                }
            }
        }

        await _next(context);
    }
}
