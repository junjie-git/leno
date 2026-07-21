namespace Leno.UserAuth.Application.Abstractions;

/// <summary>
/// OAuth2 全局配置选项。
/// <see cref="PublicBaseUrl"/> 用于构造回调 URL，避免直接信任 <c>Request.Host</c> 导致 Host Header 注入。
/// <see cref="AllowedRedirectUris"/> 为开放重定向防护白名单，客户端传入的 redirectUri 必须命中白名单。
/// </summary>
public sealed class OAuth2Options
{
    /// <summary>
    /// 对外可访问的基础 URL（如 <c>https://api.leno.com</c>），用于构造 OAuth2 回调地址。
    /// 生产环境必须配置且为 HTTPS；开发环境可留空，回退使用 <c>Request.Host</c>。
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// 允许的 redirectUri 白名单（精确匹配，大小写不敏感）。
    /// 防止攻击者构造 <c>?redirectUri=https://evil.com/callback</c> 钓鱼链接。
    /// </summary>
    public IList<string> AllowedRedirectUris { get; set; } = new List<string>();
}
