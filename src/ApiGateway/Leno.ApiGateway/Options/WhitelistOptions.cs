namespace Leno.ApiGateway.Options;

/// <summary>
/// T26：白名单路由配置选项，对应 appsettings.json 中 <c>Gateway:Whitelist</c> 节。
/// <para>
/// 白名单路径下的请求跳过认证拦截（如 login/register/refresh-token/health/metrics）。
/// 匹配规则：请求路径以任一 <see cref="Paths"/> 项为前缀（OrdinalIgnoreCase）则放行。
/// </para>
/// </summary>
public sealed class WhitelistOptions
{
    public const string SectionName = "Gateway:Whitelist";

    /// <summary>
    /// 白名单路径前缀集合。默认包含认证、健康检查、指标端点。
    /// 业务侧可通过 appsettings.json <c>Gateway:Whitelist:Paths</c> 覆盖或扩展。
    /// </summary>
    public List<string> Paths { get; set; } = new()
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/refresh-token",
        "/health",
        "/metrics"
    };

    /// <summary>
    /// 判断给定路径是否命中白名单。null/空路径直接返回 false。
    /// 使用 <see cref="StringComparison.OrdinalIgnoreCase"/> 匹配，与原内联 lambda 行为一致。
    /// </summary>
    public bool IsWhitelisted(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (var prefix in Paths)
        {
            if (!string.IsNullOrEmpty(prefix)
                && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
