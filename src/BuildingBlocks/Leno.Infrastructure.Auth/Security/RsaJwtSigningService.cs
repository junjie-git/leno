using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Leno.Infrastructure.Security;

/// <summary>
/// RS256 JWT 签名服务实现（3.10 安全技术栈升级）。
/// <para>
/// 双轨下线 A6（2026-09-23，D-6 单算法）：HS256 与 Dual 过渡模式已删除 ——
/// 本服务<b>只做 RS256</b>：经 <see cref="IKeyManagementService"/> 获取 KMS 托管的 RSA 私钥签名，
/// 公钥验签；验签参数与 ASP.NET Core JwtBearer（JWKS 路径）保持一致。
/// </para>
/// </summary>
public sealed class RsaJwtSigningService : IJwtSigningService
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private readonly IKeyManagementService _kms;
    private readonly JwtSigningOptions _options;
    private readonly ILogger<RsaJwtSigningService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    private SigningCredentials? _cachedRs256Credentials;
    private RsaSecurityKey? _cachedRsaPublicKey;
    private readonly object _credentialsLock = new();

    public RsaJwtSigningService(
        IKeyManagementService kms,
        IOptions<JwtSigningOptions> options,
        ILogger<RsaJwtSigningService> logger)
    {
        ArgumentNullException.ThrowIfNull(kms);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _kms = kms;
        _options = options.Value ?? new JwtSigningOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> SignAsync(JwtPayload payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var credentials = await GetOrCreateRs256CredentialsAsync(ct).ConfigureAwait(false);

        var header = new JwtHeader(credentials);
        var token = new JwtSecurityToken(header, payload);
        return _tokenHandler.WriteToken(token);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return await TryVerifyWithRsaAsync(token, ct).ConfigureAwait(false);
    }

    private async Task<SigningCredentials> GetOrCreateRs256CredentialsAsync(CancellationToken ct)
    {
        lock (_credentialsLock)
        {
            if (_cachedRs256Credentials is not null)
            {
                return _cachedRs256Credentials;
            }
        }

        var rsa = await _kms.GetPrivateKeyAsync(_options.CurrentKeyId, ct).ConfigureAwait(false);
        // KeyId 作为 kid 头写入 JWT，便于验签方路由到正确密钥版本
        var key = new RsaSecurityKey(rsa) { KeyId = _options.CurrentKeyId };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        lock (_credentialsLock)
        {
            _cachedRs256Credentials ??= credentials;
            return _cachedRs256Credentials;
        }
    }

    private async Task<RsaSecurityKey> GetOrCreateRsaPublicKeyAsync(CancellationToken ct)
    {
        lock (_credentialsLock)
        {
            if (_cachedRsaPublicKey is not null)
            {
                return _cachedRsaPublicKey;
            }
        }

        var rsa = await _kms.GetPublicKeyAsync(_options.CurrentKeyId, ct).ConfigureAwait(false);
        var key = new RsaSecurityKey(rsa) { KeyId = _options.CurrentKeyId };

        lock (_credentialsLock)
        {
            _cachedRsaPublicKey ??= key;
            return _cachedRsaPublicKey;
        }
    }

    private async Task<bool> TryVerifyWithRsaAsync(string token, CancellationToken ct)
    {
        try
        {
            var publicKey = await GetOrCreateRsaPublicKeyAsync(ct).ConfigureAwait(false);
            var parameters = BuildValidationParameters(publicKey);
            var result = await _tokenHandler.ValidateTokenAsync(token, parameters).ConfigureAwait(false);
            return result.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RS256 验签失败");
            return false;
        }
    }

    private TokenValidationParameters BuildValidationParameters(SecurityKey signingKey)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ClockSkew = ClockSkew,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };
    }
}
