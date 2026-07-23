using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Leno.Identity.Domain.Aggregates;
using Leno.SharedContracts.Grpc.AccessControl.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Leno.Identity.Application.Services;

/// <summary>
/// JWT 访问令牌与刷新令牌生成服务（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// <para>
/// 核心职责：通过 AccessControl BC 的 <c>GetUserRoles</c> gRPC RPC 获取用户角色，
/// 将角色写入 JWT claims（同时填充 <see cref="ClaimTypes.Role"/> 与 <c>"role"</c> 两种类型，
/// 兼容 ASP.NET Core RBAC 与网关自定义校验）。
/// </para>
/// <para>
/// 签名算法：HS256（对称密钥，阶段四升级为 RS256 非对称密钥）。
/// </para>
/// <para>
/// 依赖：<see cref="AccessControlService.AccessControlServiceClient"/>（由 Infrastructure 层
/// <c>AddGrpcClient</c> 注册）、<see cref="JwtOptions"/> 配置、<see cref="ILogger{TCategoryName}"/>。
/// </para>
/// </summary>
public sealed class JwtTokenService
{
    /// <summary>HS256 要求 SymmetricSecurityKey 至少 256 位（32 字节）。</summary>
    private const int MinSigningKeyBytes = 32;

    /// <summary>不透明刷新令牌的字节长度（256 位熵）。</summary>
    private const int RefreshTokenByteLength = 32;

    /// <summary>JWT 时钟偏移容忍窗口（与共享内核 JwtBearer 管线一致，缩短吊销生效窗口）。</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private readonly JwtOptions _options;
    private readonly AccessControlService.AccessControlServiceClient _accessControlClient;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    /// <summary>
    /// 初始化 <see cref="JwtTokenService"/> 的新实例。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// 配置节缺失或 <see cref="JwtOptions.SigningKey"/> 长度不足 32 字节。
    /// </exception>
    public JwtTokenService(
        IOptions<JwtOptions> options,
        AccessControlService.AccessControlServiceClient accessControlClient,
        ILogger<JwtTokenService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(accessControlClient);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value ?? throw new InvalidOperationException("Identity:Jwt 配置节缺失或绑定失败");
        _accessControlClient = accessControlClient;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException(
                "Identity:Jwt:SigningKey 配置缺失，无法签发 JWT。请提供至少 32 字节的随机密钥。");
        }

        var keyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);
        if (keyBytes.Length < MinSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Identity:Jwt:SigningKey 长度不足：HS256 要求至少 {MinSigningKeyBytes} 字节（256 位），" +
                $"当前 UTF-8 编码仅 {keyBytes.Length} 字节。请使用更长的随机密钥。");
        }

        if (_options.AccessTokenExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                $"Identity:Jwt:AccessTokenExpirationMinutes 必须为正数，当前为 {_options.AccessTokenExpirationMinutes}。");
        }

        if (_options.RefreshTokenExpirationDays <= 0)
        {
            throw new InvalidOperationException(
                $"Identity:Jwt:RefreshTokenExpirationDays 必须为正数，当前为 {_options.RefreshTokenExpirationDays}。");
        }
    }

    /// <summary>
    /// 当前时刻计算的访问令牌过期时间（UTC），与令牌签发逻辑一致。
    /// </summary>
    public DateTime AccessTokenExpiresAt => DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);

    /// <summary>
    /// 生成访问令牌（JWT）。
    /// 流程：
    /// <list type="number">
    /// <item>调用 AccessControl BC <c>GetUserRoles</c> RPC 获取用户角色编码列表。</item>
    /// <item>构建 claims：sub/jti/nameidentifier/name/email + 每个角色的 Role 与 "role" 双声明。</item>
    /// <item>HS256 签名，签发 JWT。</item>
    /// </list>
    /// gRPC 调用失败时记录错误并向上抛出，由调用方决定降级策略（避免签发无角色的令牌导致权限异常）。
    /// </summary>
    /// <param name="user">用户聚合根。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>JWT 字符串。</returns>
    public async Task<string> GenerateAccessToken(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        GetUserRolesResponse rolesResponse;
        try
        {
            rolesResponse = await _accessControlClient.GetUserRolesAsync(
                new GetUserRolesRequest { UserId = user.Id.ToString("D") },
                cancellationToken: ct).ConfigureAwait(false);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "调用 AccessControl BC GetUserRoles RPC 失败，UserId={UserId}, StatusCode={StatusCode}",
                user.Id, ex.StatusCode);
            throw;
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            claims.Add(new Claim(ClaimTypes.MobilePhone, user.PhoneNumber));
        }

        foreach (var role in rolesResponse.Roles)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                // 同时添加 ClaimTypes.Role 与 "role" 双声明，兼容 ASP.NET Core RBAC（User.IsInRole）与网关自定义校验
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// 生成不透明刷新令牌聚合根。
    /// 令牌字符串为 Base64URL 编码的 32 字节随机数；过期时间由 <see cref="JwtOptions.RefreshTokenExpirationDays"/> 决定。
    /// </summary>
    /// <param name="userId">所属用户标识。</param>
    /// <returns>新建的 <see cref="RefreshToken"/> 聚合根（未持久化）。</returns>
    public RefreshToken GenerateRefreshToken(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        var bytes = new byte[RefreshTokenByteLength];
        RandomNumberGenerator.Fill(bytes);
        var tokenString = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var expiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays);
        return RefreshToken.Create(Guid.NewGuid(), tokenString, userId, expiresAt);
    }

    /// <summary>
    /// 构造与共享内核 JwtBearer 管线一致的校验参数，供验证场景复用。
    /// </summary>
    public TokenValidationParameters BuildValidationParameters()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = ClockSkew,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };
    }
}
