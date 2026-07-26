namespace Leno.Identity.Application.Abstractions;

/// <summary>
/// OAuth2 配置选项（Identity BC）。
/// 承载 redirectUri 白名单等安全配置，防止开放重定向攻击。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class OAuth2Options
{
    /// <summary>
    /// 允许的 redirectUri 白名单。空集合表示不校验（开发环境）。
    /// 生产环境必须配置，防止开放重定向攻击（P1-8）。
    /// </summary>
    public IReadOnlyList<string> AllowedRedirectUris { get; set; } = Array.Empty<string>();
}
