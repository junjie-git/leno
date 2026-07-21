using Leno.ApiGateway.Options;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Middleware;

/// <summary>
/// T26：白名单路由 + 未认证拦截中间件，在 <c>UseAuthentication</c> 之后、<c>UseAuthorization</c> 之前。
/// <para>
/// 提取自原 Program.cs 中内联 lambda，便于单元测试与配置化。
/// 行为契约（与原内联 lambda 保持一致）：
/// <list type="bullet">
///   <item>请求路径命中 <see cref="WhitelistOptions"/> 白名单 → 直接放行。</item>
///   <item>未命中白名单且用户未认证 → 返回 401 + JSON <c>{ code=401, message="未认证" }</c>。</item>
///   <item>未命中白名单但已认证 → 放行到下游中间件。</item>
/// </list>
/// </para>
/// <para>
/// 使用 <see cref="IOptionsMonitor{TOptions}"/> 而非 <see cref="IOptions{TOptions}"/>，
/// 以支持白名单路径在运行时通过配置文件热更新（如 Consul KV 推送新路径）。
/// </para>
/// </summary>
public sealed class WhitelistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<WhitelistOptions> _options;

    public WhitelistMiddleware(RequestDelegate next, IOptionsMonitor<WhitelistOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var options = _options.CurrentValue;

        if (options.IsWhitelisted(path))
        {
            await _next(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { code = 401, message = "未认证" });
            return;
        }

        await _next(context);
    }
}
