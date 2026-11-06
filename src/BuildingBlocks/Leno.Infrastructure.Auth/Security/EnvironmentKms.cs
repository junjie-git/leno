using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Security;

/// <summary>
/// 基于环境变量/配置的 KMS 回退实现（3.10 安全技术栈升级 / DG-4 务实推进）。
/// <para>
/// 当实际 KMS 实例（Azure Key Vault 等）不可用时作为默认实现。
/// RSA 密钥从 <see cref="JwtSigningOptions"/> 或环境变量读取 PEM 格式字符串。
/// 不依赖外部 KMS 服务，适合开发环境与 CI/CD。
/// </para>
/// <para>
/// 环境变量优先级：
/// <list type="bullet">
/// <item><c>JWT_RSA_PRIVATE_KEY_PEM</c>：RSA 私钥 PEM（含 -----BEGIN RSA PRIVATE KEY----- 或 -----BEGIN PRIVATE KEY-----）</item>
/// <item><c>JWT_RSA_PUBLIC_KEY_PEM</c>：RSA 公钥 PEM</item>
/// </list>
/// 若环境变量未设置，回退到 <see cref="JwtSigningOptions.RsaPrivateKeyPem"/> / <see cref="JwtSigningOptions.RsaPublicKeyPem"/>。
/// </para>
/// </summary>
public sealed class EnvironmentKms : IKeyManagementService
{
    private const string PrivateKeyEnvVar = "JWT_RSA_PRIVATE_KEY_PEM";
    private const string PublicKeyEnvVar = "JWT_RSA_PUBLIC_KEY_PEM";

    private readonly JwtSigningOptions _options;
    private readonly ILogger<EnvironmentKms> _logger;
    private RSA? _cachedPrivateKey;
    private RSA? _cachedPublicKey;
    private readonly object _lock = new();

    public EnvironmentKms(IOptions<JwtSigningOptions> options, ILogger<EnvironmentKms> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value ?? new JwtSigningOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<RSA> GetPrivateKeyAsync(string keyId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new ArgumentException("KeyId 不可为空", nameof(keyId));
        }

        lock (_lock)
        {
            if (_cachedPrivateKey is not null)
            {
                return Task.FromResult(_cachedPrivateKey);
            }

            var pem = ResolvePem(PrivateKeyEnvVar, _options.RsaPrivateKeyPem);
            if (string.IsNullOrWhiteSpace(pem))
            {
                throw new InvalidOperationException(
                    $"RSA 私钥未配置。请设置环境变量 {PrivateKeyEnvVar} 或配置 JwtSigning:RsaPrivateKeyPem。" +
                    "可使用 'openssl genpkey -algorithm RSA -out private.pem -pkeyopt rsa_keygen_bits:2048' 生成。");
            }

            _cachedPrivateKey = RSA.Create();
            _cachedPrivateKey.ImportFromPem(pem);

            _logger.LogInformation("EnvironmentKms: RSA 私钥已加载，KeyId={KeyId}", keyId);
            return Task.FromResult(_cachedPrivateKey);
        }
    }

    /// <inheritdoc />
    public Task<RSA> GetPublicKeyAsync(string keyId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new ArgumentException("KeyId 不可为空", nameof(keyId));
        }

        lock (_lock)
        {
            if (_cachedPublicKey is not null)
            {
                return Task.FromResult(_cachedPublicKey);
            }

            var pem = ResolvePem(PublicKeyEnvVar, _options.RsaPublicKeyPem);
            if (!IsPem(pem))
            {
                // 公钥未单独配置（或配置的是未被展开的占位符，如 "${JWT_RSA_PUBLIC_KEY_PEM}"）时，从私钥推导
                if (_cachedPrivateKey is null)
                {
                    var privPem = ResolvePem(PrivateKeyEnvVar, _options.RsaPrivateKeyPem);
                    if (!IsPem(privPem))
                    {
                        throw new InvalidOperationException(
                            $"RSA 公钥与私钥均未配置。请设置环境变量 {PublicKeyEnvVar} 或 {PrivateKeyEnvVar}。");
                    }

                    _cachedPrivateKey = RSA.Create();
                    _cachedPrivateKey.ImportFromPem(privPem);
                }

                _cachedPublicKey = RSA.Create();
                _cachedPublicKey.ImportParameters(_cachedPrivateKey.ExportParameters(includePrivateParameters: false));
            }
            else
            {
                _cachedPublicKey = RSA.Create();
                _cachedPublicKey.ImportFromPem(pem);
            }

            _logger.LogInformation("EnvironmentKms: RSA 公钥已加载，KeyId={KeyId}", keyId);
            return Task.FromResult(_cachedPublicKey);
        }
    }

    /// <inheritdoc />
    public async Task<string> WrapAesKeyAsync(byte[] plaintextKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plaintextKey);

        var publicKey = await GetPublicKeyAsync(_options.CurrentKeyId, ct).ConfigureAwait(false);
        var wrapped = publicKey.Encrypt(plaintextKey, RSAEncryptionPadding.OaepSHA256);
        return Convert.ToBase64String(wrapped);
    }

    /// <inheritdoc />
    public async Task<byte[]> UnwrapAesKeyAsync(string wrappedKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(wrappedKey))
        {
            throw new ArgumentException("包装密钥不可为空", nameof(wrappedKey));
        }

        var privateKey = await GetPrivateKeyAsync(_options.CurrentKeyId, ct).ConfigureAwait(false);
        var wrappedBytes = Convert.FromBase64String(wrappedKey);
        return privateKey.Decrypt(wrappedBytes, RSAEncryptionPadding.OaepSHA256);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListKeyVersionsAsync(string keyName, CancellationToken ct)
    {
        // 环境变量模式仅支持单一版本，返回配置的 CurrentKeyId
        var versions = new List<string> { _options.CurrentKeyId };
        return Task.FromResult<IReadOnlyList<string>>(versions);
    }

    private static string ResolvePem(string envVar, string fallback)
    {
        var envValue = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return NormalizePem(envValue);
        }

        return NormalizePem(fallback ?? string.Empty);
    }

    /// <summary>
    /// 还原以单行形态承载的 PEM：环境变量/配置中心常把换行写成 <c>\n</c> 转义，
    /// 而 <see cref="RSA.ImportFromPem(string)"/> 要求真实换行，这里统一转换；
    /// 已包含真实换行的标准 PEM 不受影响。
    /// </summary>
    private static string NormalizePem(string pem)
        => pem.Replace("\\n", "\n", StringComparison.Ordinal).Trim();

    /// <summary>
    /// 判断配置值是否为可导入的 PEM 文本。
    /// 除空值外，未展开的配置占位符（<c>${...}</c>）同样视为"未配置"，
    /// 否则 <see cref="RSA.ImportFromPem(string)"/> 会抛出与真实原因无关的格式错误。
    /// </summary>
    private static bool IsPem(string pem)
        => !string.IsNullOrWhiteSpace(pem)
            && pem.Contains("-----BEGIN", StringComparison.Ordinal);
}
