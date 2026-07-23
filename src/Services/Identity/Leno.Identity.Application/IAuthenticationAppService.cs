using Leno.Identity.Application.DTOs;

namespace Leno.Identity.Application;

/// <summary>
/// 认证应用服务接口，编排登录、刷新与登出用例（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// 所有方法在事务边界内提交聚合变更与领域事件（经 Outbox 持久化）。
/// 角色信息由 <see cref="Services.JwtTokenService"/> 通过 AccessControl BC <c>GetUserRoles</c> RPC 获取，
/// Identity BC 本身不再持久化角色数据。
/// </summary>
public interface IAuthenticationAppService
{
    /// <summary>
    /// 账号密码登录。
    /// 流程：按用户名/邮箱查找用户 → 校验账户可登录 → 验证密码 → 重置失败计数 →
    /// 签发刷新令牌 → 发布 <c>UserAuthenticatedEvent</c> → 生成访问令牌（含角色 claims）。
    /// </summary>
    /// <param name="dto">登录请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含访问与刷新令牌的响应。</returns>
    Task<TokenDto> LoginAsync(LoginDto dto, CancellationToken ct = default);

    /// <summary>
    /// 刷新令牌轮换。
    /// 流程：校验刷新令牌有效 → 撤销旧令牌并记录替换关系 → 签发新刷新令牌 → 生成新访问令牌。
    /// 遵循 RFC 6749 §10.4 的令牌轮换安全实践，防止重放。
    /// </summary>
    /// <param name="dto">刷新请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含新访问与新刷新令牌的响应。</returns>
    Task<TokenDto> RefreshAsync(RefreshTokenDto dto, CancellationToken ct = default);

    /// <summary>
    /// 登出，吊销指定用户的所有活跃刷新令牌。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task LogoutAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// OAuth2 / OIDC / SAML2 回调处理（3.7 OAuth/SSO 通用化）。
    /// <para>
    /// 流程：按 provider slug 查找 <c>OAuthClient</c> 配置 → 通过 <c>ProviderType</c> 解析适配器 →
    /// 交换授权码 → 拉取 IdP userinfo → 映射 claim 为 <c>ClaimsPrincipal</c> →
    /// 按 <c>(Provider, ProviderUserId)</c> 查找已绑定用户，未找到则自动创建（无密码、无手机号的 OAuth 用户） →
    /// 签发刷新令牌 → 生成访问令牌。
    /// </para>
    /// </summary>
    /// <param name="provider">OAuthClient.Provider slug，如 google / wechat / keycloak / 自定义 IdP 标识。</param>
    /// <param name="code">回调返回的授权码（OIDC）或 SAMLResponse（SAML2，Base64 编码）。</param>
    /// <param name="redirectUri">回调地址，必须与发起授权时一致。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含访问与刷新令牌的响应。</returns>
    Task<TokenDto> HandleOAuthCallbackAsync(string provider, string code, string redirectUri, CancellationToken ct = default);
}
