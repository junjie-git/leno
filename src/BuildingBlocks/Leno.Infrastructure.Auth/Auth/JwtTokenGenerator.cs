using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Auth;

/// <summary>
/// JWT 配置，对应 appsettings.json 中 <c>Jwt</c> 节。
/// <para>
/// 双轨下线 A6（2026-09-23，D-4/D-6）：<b>RS256-only，零信任</b> ——
/// 删除 <c>SecretKey</c>（HS256 共享对称密钥）与网关透传模式；
/// 各服务通过 <see cref="DiscoveryUrl"/> 拉取 Identity 的 OIDC 发现文档与 JWKS 公钥本地验签。
/// </para>
/// </summary>
public sealed class JwtOptions
{
    /// <summary>JWT 发行方标识（须与 Identity 签发一致）。</summary>
    public string Issuer { get; set; } = "leno-identity";

    /// <summary>JWT 受众标识（须与 Identity 签发一致）。</summary>
    public string Audience { get; set; } = "leno-clients";

    /// <summary>
    /// Identity 的 OIDC 发现文档地址（如 <c>http://leno-identity-api:8080/.well-known/openid-configuration</c>）。
    /// JwtBearer 据此自动拉取 <c>jwks_uri</c> 并缓存签名公钥；RS256 验签的唯一密钥来源。
    /// </summary>
    public string DiscoveryUrl { get; set; } = string.Empty;

    /// <summary>
    /// 拉取发现文档/JWKS 是否要求 HTTPS。内网明文部署设 false（默认）；生产启用 TLS 后设 true。
    /// </summary>
    public bool RequireHttpsMetadata { get; set; }

    /// <summary>访问令牌有效期（分钟），默认 120 分钟。</summary>
    public int AccessTokenExpiryMinutes { get; set; } = 120;

    /// <summary>刷新令牌有效期（天），默认 7 天。</summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;
}

/// <summary>
/// JWT 辅助工具：Claim 读取、刷新令牌生成与过期时间。
/// <para>
/// 双轨下线 A6（2026-09-23）：本类原有的 <b>HS256 签发</b>（<c>GenerateAccessToken</c>）与
/// <b>HS256 校验</b>（<c>ValidateTokenAsync</c> / <c>BuildValidationParameters</c>）已删除 ——
/// 令牌只能由 Identity 以 RS256 签发，各服务经 JWKS 本地验签（见 <see cref="JwtOptions.DiscoveryUrl"/>）。
/// 保留的静态助手被 <see cref="CurrentUserContext"/> 等广泛使用。
/// </para>
/// </summary>
public sealed class JwtTokenGenerator
{
    /// <summary>ShopId 自定义 Claim 类型。</summary>
    public const string ShopIdClaimType = "shop_id";

    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new InvalidOperationException("JwtOptions 未配置");
    }

    /// <summary>
    /// 生成不透明的刷新令牌字符串（与访问令牌独立）。
    /// </summary>
    public static string GenerateRefreshToken()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>刷新令牌有效期。</summary>
    public TimeSpan RefreshTokenExpiry => TimeSpan.FromDays(_options.RefreshTokenExpiryDays);

    /// <summary>从 ClaimsPrincipal 提取 UserId。</summary>
    public static Guid? GetUserId(ClaimsPrincipal? principal)
    {
        var claim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>从 ClaimsPrincipal 提取 Role。</summary>
    public static string? GetRole(ClaimsPrincipal? principal)
        => principal?.FindFirst(ClaimTypes.Role)?.Value ?? principal?.FindFirst("role")?.Value;

    /// <summary>从 ClaimsPrincipal 提取 ShopId。</summary>
    public static Guid? GetShopId(ClaimsPrincipal? principal)
    {
        var claim = principal?.FindFirst(ShopIdClaimType)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>从 ClaimsPrincipal 提取 SessionId（JWT jti claim）。</summary>
    public static string? GetSessionId(ClaimsPrincipal? principal)
        => principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
}
