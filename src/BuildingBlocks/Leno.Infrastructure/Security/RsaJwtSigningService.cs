using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Leno.Infrastructure.Security;

/// <summary>
/// RS256 JWT 签名服务实现（3.10 安全技术栈升级 / HS256 → RS256 过渡）。
/// <para>
/// 签名模式由 <see cref="JwtSigningOptions.SigningMode"/> 控制：
/// <list type="bullet">
/// <item><b>Hs256</b>：使用 <see cref="JwtSigningOptions.Hs256SigningKey"/> 对称签名。</item>
/// <item><b>Rs256</b>：通过 <see cref="IKeyManagementService"/> 获取 RSA 私钥非对称签名。</item>
/// <item><b>Dual</b>：新令牌使用 RS256 签名，验签同时接受 RS256 与 HS256（过渡兼容）。</item>
/// </list>
/// </para>
/// </summary>
public sealed class RsaJwtSigningService : IJwtSigningService
{
    private const int MinHs256KeyBytes = 32;
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private readonly IKeyManagementService _kms;
    private readonly JwtSigningOptions _options;
    private readonly ILogger<RsaJwtSigningService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    private SigningCredentials? _cachedHs256Credentials;
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

        var mode = NormalizeMode(_options.SigningMode);
        var credentials = mode switch
        {
            SigningModeValue.Hs256 => GetOrCreateHs256Credentials(),
            SigningModeValue.Rs256 => await GetOrCreateRs256CredentialsAsync(ct).ConfigureAwait(false),
            SigningModeValue.Dual => await GetOrCreateRs256CredentialsAsync(ct).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"不支持的签名模式：{_options.SigningMode}")
        };

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

        var mode = NormalizeMode(_options.SigningMode);

        // RS256 验签（RS256 和 Dual 模式优先尝试 RS256 验签）
        if (mode is SigningModeValue.Rs256 or SigningModeValue.Dual)
        {
            if (await TryVerifyWithRsaAsync(token, ct).ConfigureAwait(false))
            {
                return true;
            }
        }

        // HS256 验签（Hs256 模式 + Dual 模式 RS256 失败时回退）
        if (mode is SigningModeValue.Hs256 or SigningModeValue.Dual)
        {
            return TryVerifyWithHs256(token);
        }

        return false;
    }

    private SigningCredentials GetOrCreateHs256Credentials()
    {
        lock (_credentialsLock)
        {
            if (_cachedHs256Credentials is not null)
            {
                return _cachedHs256Credentials;
            }

            if (string.IsNullOrWhiteSpace(_options.Hs256SigningKey))
            {
                throw new InvalidOperationException(
                    "JwtSigning:Hs256SigningKey 配置缺失，HS256 模式需要至少 32 字节的对称密钥。");
            }

            var keyBytes = Encoding.UTF8.GetBytes(_options.Hs256SigningKey);
            if (keyBytes.Length < MinHs256KeyBytes)
            {
                throw new InvalidOperationException(
                    $"JwtSigning:Hs256SigningKey 长度不足：HS256 要求至少 {MinHs256KeyBytes} 字节，当前 {keyBytes.Length} 字节。");
            }

            var key = new SymmetricSecurityKey(keyBytes) { KeyId = "hs256" };
            _cachedHs256Credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            return _cachedHs256Credentials;
        }
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

    private bool TryVerifyWithHs256(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_options.Hs256SigningKey))
            {
                return false;
            }

            var keyBytes = Encoding.UTF8.GetBytes(_options.Hs256SigningKey);
            if (keyBytes.Length < MinHs256KeyBytes)
            {
                return false;
            }

            var key = new SymmetricSecurityKey(keyBytes);
            var parameters = BuildValidationParameters(key);
            var result = _tokenHandler.ValidateTokenAsync(token, parameters).GetAwaiter().GetResult();
            return result.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HS256 验签失败（Dual 回退）");
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

    private static SigningModeValue NormalizeMode(string? mode)
    {
        return mode?.ToLowerInvariant() switch
        {
            "hs256" => SigningModeValue.Hs256,
            "rs256" => SigningModeValue.Rs256,
            "dual" => SigningModeValue.Dual,
            _ => SigningModeValue.Hs256
        };
    }

    private enum SigningModeValue
    {
        Hs256,
        Rs256,
        Dual
    }
}
