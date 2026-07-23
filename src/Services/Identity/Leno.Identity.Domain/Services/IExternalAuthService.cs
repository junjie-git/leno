using Leno.Identity.Domain.ValueObjects;

namespace Leno.Identity.Domain.Services;

/// <summary>
/// 外部 OAuth2 身份认证服务抽象，定义第三方登录的统一契约。
/// 每个实现映射一个 Provider 标识（google / wechat / alipay），
/// 由 OAuth2ProviderResolver 根据 Provider 字符串解析。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public interface IExternalAuthService
{
    /// <summary>OAuth2 提供方标识，如 google / wechat / alipay。</summary>
    string Provider { get; }

    /// <summary>
    /// 构建第三方授权页面 URL，附带 state 防 CSRF 与 redirectUri 回调地址。
    /// </summary>
    string GetAuthorizationUrl(string state, string redirectUri);

    /// <summary>
    /// 用授权码换取访问令牌，并解析用户信息为 <see cref="ExternalLoginInfo"/>。
    /// </summary>
    Task<ExternalLoginInfo> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct = default);

    /// <summary>
    /// 使用访问令牌获取第三方用户信息（用于静默登录或令牌续期场景）。
    /// </summary>
    Task<ExternalLoginInfo> GetUserInfoAsync(string accessToken, CancellationToken ct = default);
}
