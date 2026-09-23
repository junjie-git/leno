using System.Security.Cryptography;
using Leno.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Leno.Identity.Api.Controllers;

/// <summary>
/// OIDC/JWKS 元数据端点（双轨下线 A6 · RS256 阶段 2/3，2026-09-23）。
/// <para>
/// 发布两样东西，供各服务<b>本地验签</b>（零信任：服务不共享对称密钥、不信任网关注入的头）：
/// <list type="bullet">
/// <item><c>/.well-known/openid-configuration</c>：最小 OIDC 发现文档，含 <c>jwks_uri</c> ——
/// 消费方在 JwtBearer 的 <c>MetadataAddress</c> 指向它，框架自动拉取并缓存签名公钥。</item>
/// <item><c>/.well-known/jwks.json</c>：RFC 7517 JWKS，发布当前 RSA 公钥与轮换重叠期的上一把公钥
/// （<see cref="JwtSigningOptions.PreviousKeyIds"/>，供密钥轮换四步法的第 2 步）。</item>
/// </list>
/// 公钥来自 <see cref="IKeyManagementService"/>（开发/CI：环境变量 PEM；生产：Azure Key Vault），
/// 与 Identity 签发所用私钥同源，因此发布即验签一致。
/// </para>
/// <para>
/// 输出缓存 5 分钟（P2 改进）：JWKS 内容只在轮换时变化，代理层可安全缓存，
/// 降低轮换外的 Identity 请求压力；消费方另有 1h 自动刷新兜底。
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class JwksController : ControllerBase
{
    private const string SigningAlgorithm = "RS256";

    private readonly IKeyManagementService _kms;
    private readonly JwtSigningOptions _options;
    private readonly ILogger<JwksController> _logger;

    public JwksController(IKeyManagementService kms, IOptions<JwtSigningOptions> options, ILogger<JwksController> logger)
    {
        ArgumentNullException.ThrowIfNull(kms);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _kms = kms;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// JWKS 文档：当前 RSA 公钥 + 轮换重叠期的上一把公钥（n/e，Base64URL 无填充）。
    /// 某一把旧钥在 KMS 中不可用时跳过并告警（轮换第 4 步移除后属预期），当前钥不可用则拒绝。
    /// </summary>
    [HttpGet(".well-known/jwks.json")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJwksAsync(CancellationToken ct)
    {
        var keyIds = BuildPublishedKeyIds();
        var keys = new List<object>();

        foreach (var keyId in keyIds)
        {
            try
            {
                using var rsa = await _kms.GetPublicKeyAsync(keyId, ct).ConfigureAwait(false);
                var p = rsa.ExportParameters(includePrivateParameters: false);
                if (p.Modulus is null || p.Exponent is null)
                {
                    _logger.LogWarning("JWKS：KMS 返回的 RSA 公钥不完整（KeyId={KeyId}），跳过", keyId);
                    continue;
                }

                keys.Add(new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = SigningAlgorithm,
                    kid = keyId,
                    n = Base64Url(p.Modulus),
                    e = Base64Url(p.Exponent)
                });
            }
            catch (Exception ex) when (!string.Equals(keyId, _options.CurrentKeyId, StringComparison.Ordinal))
            {
                // 轮换重叠期旧钥尚未在 KMS 配置属预期：跳过不阻断
                _logger.LogWarning(ex, "JWKS：轮换重叠公钥不可用，跳过发布 KeyId={KeyId}", keyId);
            }
        }

        if (keys.Count == 0)
        {
            throw new InvalidOperationException(
                $"JWKS 无可发布公钥（CurrentKeyId={_options.CurrentKeyId}）。请检查 KMS 配置。");
        }

        return Ok(new { keys });
    }

    /// <summary>
    /// 最小 OIDC 发现文档：消费方 JwtBearer 的 MetadataAddress 指向本端点。
    /// <c>jwks_uri</c> 按请求主机推导，保证消费方用哪个主机访问本服务就拿到哪个主机的公钥地址。
    /// </summary>
    [HttpGet(".well-known/openid-configuration")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetDiscovery()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        return Ok(new
        {
            issuer = _options.Issuer,
            jwks_uri = $"{baseUrl}/.well-known/jwks.json",
            id_token_signing_alg_values_supported = new[] { SigningAlgorithm },
            response_types_supported = new[] { "token" },
            subject_types_supported = new[] { "public" },
            claims_supported = new[]
            {
                "sub", "jti", "name", "email", "phone_number", "role",
                System.Security.Claims.ClaimTypes.Role,
                System.Security.Claims.ClaimTypes.NameIdentifier
            }
        });
    }

    /// <summary>
    /// 发布顺序：当前钥在前，<see cref="JwtSigningOptions.PreviousKeyIds"/> 中的重叠钥随后（去重）。
    /// </summary>
    private IReadOnlyList<string> BuildPublishedKeyIds()
    {
        var ids = new List<string> { _options.CurrentKeyId };
        foreach (var prev in (_options.PreviousKeyIds ?? string.Empty)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (prev.Length > 0 && !ids.Contains(prev, StringComparer.Ordinal))
            {
                ids.Add(prev);
            }
        }

        return ids;
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
