using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Leno.Infrastructure.Auth;

/// <summary>
/// JWT 配置，对应 appsettings.json 中 <c>Jwt</c> 节。
/// </summary>
public sealed class JwtOptions
{
    public string Issuer { get; set; } = default!;

    public string Audience { get; set; } = default!;

    public string SecretKey { get; set; } = default!;

    /// <summary>访问令牌有效期（分钟），默认 120 分钟。</summary>
    public int AccessTokenExpiryMinutes { get; set; } = 120;

    /// <summary>刷新令牌有效期（天），默认 7 天。</summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;
}

/// <summary>
/// JWT 生成与校验器。Claim 携带 UserId、Role、ShopId，签名算法 HS256。
/// </summary>
public sealed class JwtTokenGenerator
{
    /// <summary>ShopId 自定义 Claim 类型。</summary>
    public const string ShopIdClaimType = "shop_id";

    /// <summary>T23：ClockSkew 缩短为 30 秒，配合 JwtBlacklistService Pub/Sub 实时同步缩短吊销生效窗口。</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>T22：HS256 要求 SymmetricSecurityKey 至少 256 位（32 字节）。</summary>
    private const int MinSecretKeyBytes = 32;

    private readonly JwtOptions _options;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new InvalidOperationException("JwtOptions 未配置");

        // T22：校验 SymmetricSecurityKey 长度 >= 32 字节（HS256 要求 256 位密钥）
        // 短密钥在运行时抛 SecurityTokenInvalidSigningKeyException 或安全降级，构造时 fail-fast 更明确
        var keyBytes = Encoding.UTF8.GetBytes(_options.SecretKey ?? string.Empty);
        if (keyBytes.Length < MinSecretKeyBytes)
        {
            throw new InvalidOperationException(
                $"JwtOptions.SecretKey 长度不足：HS256 要求至少 {MinSecretKeyBytes} 字节（256 位），" +
                $"当前 UTF-8 编码仅 {keyBytes.Length} 字节。请使用更长的随机密钥。");
        }
    }

    /// <summary>
    /// 生成访问令牌（单角色重载，向后兼容）。
    /// </summary>
    public string GenerateAccessToken(Guid userId, string role, Guid? shopId, IDictionary<string, string>? additionalClaims = null)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Role 不可为空", nameof(role));
        }

        return GenerateAccessToken(userId, new[] { role }, shopId, additionalClaims);
    }

    /// <summary>
    /// 生成访问令牌（多角色重载）。
    /// 每个角色同时添加 <see cref="ClaimTypes.Role"/> 与 <c>"role"</c> 两种 Claim 类型，
    /// 兼容 ASP.NET Core RBAC（<c>User.IsInRole</c>）与网关自定义 role 校验。
    /// </summary>
    public string GenerateAccessToken(Guid userId, IEnumerable<string> roles, Guid? shopId, IDictionary<string, string>? additionalClaims = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(roles);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var hasRole = false;
        foreach (var role in roles)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
                hasRole = true;
            }
        }

        if (!hasRole)
        {
            throw new ArgumentException("至少需要一个有效角色", nameof(roles));
        }

        if (shopId.HasValue && shopId.Value != Guid.Empty)
        {
            claims.Add(new Claim(ShopIdClaimType, shopId.Value.ToString()));
        }

        if (additionalClaims is not null)
        {
            foreach (var pair in additionalClaims)
            {
                if (!string.IsNullOrEmpty(pair.Key))
                {
                    claims.Add(new Claim(pair.Key, pair.Value ?? string.Empty));
                }
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenExpiryMinutes),
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// 校验令牌并返回 <see cref="ClaimsPrincipal"/>，校验失败返回 null。
    /// </summary>
    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parameters = BuildValidationParameters();

        try
        {
            var result = await _tokenHandler.ValidateTokenAsync(token, parameters);
            if (!result.IsValid || result.ClaimsIdentity is null)
            {
                return null;
            }

            return new ClaimsPrincipal(result.ClaimsIdentity);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    /// <summary>
    /// 构造与 ASP.NET Core JwtBearer 一致的校验参数，供中间件/测试复用。
    /// </summary>
    public TokenValidationParameters BuildValidationParameters()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            // T23：ClockSkew 从 1 分钟缩短为 30 秒
            ClockSkew = ClockSkew,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };
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
}
