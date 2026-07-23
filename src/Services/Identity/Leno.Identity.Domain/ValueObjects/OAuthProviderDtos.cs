namespace Leno.Identity.Domain.ValueObjects;

/// <summary>
/// 授权 URL 构造结果（Identity BC，3.7 OAuth/SSO 通用化）。
/// <para>
/// 由 <c>IOAuth2ProviderAdapter.BuildAuthorizationUriAsync</c> 返回。
/// OIDC 流程中可能携带 nonce（用于后续 id_token 校验， mitigate replay attack）。
/// </para>
/// </summary>
public sealed class AuthorizationUriResult
{
    /// <summary>第三方授权页面 URL，前端跳转目标。</summary>
    public string AuthorizationUri { get; init; } = string.Empty;

    /// <summary>
    /// OIDC nonce 参数。若 IdP 流程使用 nonce，由适配器生成并随 state 一并存储在调用方，
    /// 后续 token 交换时回传（若 IdP 在 id_token 中返回 nonce）。
    /// 可空表示该协议未使用 nonce。
    /// </summary>
    public string? Nonce { get; init; }

    /// <summary>透传的 state 参数（与请求时一致），便于调用方校验回显。</summary>
    public string State { get; init; } = string.Empty;
}

/// <summary>
/// 授权码交换令牌响应（Identity BC，3.7 OAuth/SSO 通用化）。
/// <para>
/// 对应 RFC 6749 §5.1 的 token 响应结构。OIDC 流程会在标准字段之上额外返回 id_token。
/// </para>
/// </summary>
public sealed class TokenResponse
{
    /// <summary>访问令牌（Bearer），用于调用 IdP userinfo 端点。</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>令牌类型，固定 <c>Bearer</c>。</summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>过期秒数（自签发时刻起算）。</summary>
    public int ExpiresIn { get; init; }

    /// <summary>OIDC id_token（JWT），可空（非 OIDC 流程无此字段）。</summary>
    public string? IdToken { get; init; }

    /// <summary>刷新令牌，可空（offline_access scope 时返回）。</summary>
    public string? RefreshToken { get; init; }

    /// <summary>访问范围（实际授权的 scope 子集），可空。</summary>
    public string? Scope { get; init; }
}

/// <summary>
/// IdP userinfo 端点返回的原始用户信息（Identity BC，3.7 OAuth/SSO 通用化）。
/// <para>
/// 适配器调用 GetUserInfoAsync 后填充本对象。RawClaims 保留 IdP 返回的全部 claim，
/// 供 MapClaimsAsync 按映射规则转换为目标 claim。
/// </para>
/// </summary>
public sealed class UserInfoResponse
{
    /// <summary>userinfo 端点 URL（用于审计/调试）。</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// userinfo 返回的全部 claim 键值对（原始 key，未映射）。
    /// 比较器为 OrdinalIgnoreCase，允许 MapClaimsAsync 用大小写不敏感的 SourceClaim 匹配。
    /// </summary>
    public Dictionary<string, string> RawClaims { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 第三方用户唯一标识（sub claim）。所有协议均强制返回。
    /// 读取自 <see cref="RawClaims"/>["sub"]，未找到时返回 <see cref="string.Empty"/>。
    /// </summary>
    public string Subject => RawClaims.TryGetValue("sub", out var v) ? v : string.Empty;
}
