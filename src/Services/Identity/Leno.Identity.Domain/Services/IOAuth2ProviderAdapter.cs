using System.Security.Claims;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.ValueObjects;

namespace Leno.Identity.Domain.Services;

/// <summary>
/// OAuth2 / OIDC / SAML2 提供方适配器抽象（Identity BC，3.7 OAuth/SSO 通用化）。
/// <para>
/// 每个实现映射一种 <see cref="ProviderType"/>（Oidc / Google / WeChat / Saml2），
/// 由 <c>OAuth2ProviderFactory</c> 按 <see cref="ProviderType"/> 字段路由解析。
/// </para>
/// <para>
/// 设计目标：<b>配置驱动而非代码驱动</b>。新增 OIDC 兼容 IdP 仅需在 OAuthClient 表中插入一条
/// ProviderType=Oidc 的记录并填入 discovery URL，无需新增适配器类。
/// </para>
/// </summary>
public interface IOAuth2ProviderAdapter
{
    /// <summary>协议类型标识，与 <see cref="OAuthClient.ProviderType"/> 比对（PascalCase，大小写不敏感比较）。</summary>
    string ProviderType { get; }

    /// <summary>
    /// 构造第三方授权页面 URL（authorize endpoint）。
    /// </summary>
    /// <param name="client">OAuthClient 聚合配置。</param>
    /// <param name="redirectUri">本次回调地址（可与 <see cref="OAuthClient.RedirectUri"/> 不同，由调用方按上下文传入）。</param>
    /// <param name="state">CSRF 防护 state 参数，由调用方生成并存储。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含授权 URL 与可选 nonce 的结果。</returns>
    Task<AuthorizationUriResult> BuildAuthorizationUriAsync(
        OAuthClient client,
        string redirectUri,
        string state,
        CancellationToken ct);

    /// <summary>
    /// 用授权码交换访问令牌（token endpoint）。
    /// </summary>
    /// <param name="client">OAuthClient 聚合配置。</param>
    /// <param name="code">回调返回的授权码。</param>
    /// <param name="redirectUri">必须与 <see cref="BuildAuthorizationUriAsync"/> 中一致。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含 access_token / id_token / refresh_token 的响应。</returns>
    Task<TokenResponse> ExchangeCodeForTokenAsync(
        OAuthClient client,
        string code,
        string redirectUri,
        CancellationToken ct);

    /// <summary>
    /// 使用 access_token 拉取 IdP userinfo 端点返回的原始用户信息。
    /// </summary>
    Task<UserInfoResponse> GetUserInfoAsync(
        OAuthClient client,
        string accessToken,
        CancellationToken ct);

    /// <summary>
    /// 按 <see cref="OidcClaimMapping"/> 将 userinfo 的 claim 映射为 <see cref="ClaimsPrincipal"/>。
    /// </summary>
    Task<ClaimsPrincipal> MapClaimsAsync(
        UserInfoResponse userInfo,
        OidcClaimMapping mapping,
        CancellationToken ct);
}
