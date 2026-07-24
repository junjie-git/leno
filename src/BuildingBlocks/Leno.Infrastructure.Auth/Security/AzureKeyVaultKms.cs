using System.Security.Cryptography;
using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Security;

/// <summary>
/// Azure Key Vault KMS 实现（3.10 安全技术栈升级 / 生产配置）。
/// <para>
/// 完整实现所有 <see cref="IKeyManagementService"/> 方法。
/// <b>注意：实际 Azure Key Vault 实例连接标记待生产验证（DG-4）。</b>
/// 本地开发/CI 环境应使用 <see cref="EnvironmentKms"/> 作为回退。
/// </para>
/// <para>
/// HSM 支持的 RSA 密钥私钥不可导出，<see cref="GetPrivateKeyAsync"/> 返回
/// <see cref="KeyVaultRsa"/> 适配器，将签名/解密操作委托给 <see cref="CryptographyClient"/>。
/// </para>
/// </summary>
public sealed class AzureKeyVaultKms : IKeyManagementService
{
    private readonly KeyClient _keyClient;
    private readonly TokenCredential _credential;
    private readonly JwtSigningOptions _options;
    private readonly ILogger<AzureKeyVaultKms> _logger;

    public AzureKeyVaultKms(
        Uri vaultUri,
        TokenCredential credential,
        IOptions<JwtSigningOptions> options,
        ILogger<AzureKeyVaultKms> logger)
    {
        ArgumentNullException.ThrowIfNull(vaultUri);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _keyClient = new KeyClient(vaultUri, credential);
        _credential = credential;
        _options = options.Value ?? new JwtSigningOptions();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RSA> GetPrivateKeyAsync(string keyId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new ArgumentException("KeyId 不可为空", nameof(keyId));
        }

        var (keyName, version) = ParseKeyId(keyId);
        var keyResponse = await _keyClient.GetKeyAsync(keyName, version, ct).ConfigureAwait(false);
        var keyVaultKey = keyResponse.Value;

        var cryptoClient = new CryptographyClient(keyVaultKey.Id, _credential);

        // 获取公钥参数（HSM 密钥的公钥部分始终可导出）
        var publicKey = keyVaultKey.Key.ToRSA() ?? throw new InvalidOperationException(
            $"Key Vault 密钥 {keyName} 不是 RSA 密钥");
        var publicParams = publicKey.ExportParameters(includePrivateParameters: false);
        publicKey.Dispose();

        _logger.LogInformation("AzureKeyVaultKms: RSA 私钥已加载（HSM 适配器），KeyId={KeyId}", keyId);
        return new KeyVaultRsa(cryptoClient, publicParams, keyVaultKey.Key.N?.Length * 8 ?? 2048);
    }

    /// <inheritdoc />
    public async Task<RSA> GetPublicKeyAsync(string keyId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new ArgumentException("KeyId 不可为空", nameof(keyId));
        }

        var (keyName, version) = ParseKeyId(keyId);
        var keyResponse = await _keyClient.GetKeyAsync(keyName, version, ct).ConfigureAwait(false);
        var keyVaultKey = keyResponse.Value;

        var rsa = keyVaultKey.Key.ToRSA();
        if (rsa is null)
        {
            throw new InvalidOperationException($"Key Vault 密钥 {keyName} 不是 RSA 密钥");
        }

        _logger.LogInformation("AzureKeyVaultKms: RSA 公钥已加载，KeyId={KeyId}", keyId);
        return rsa;
    }

    /// <inheritdoc />
    public async Task<string> WrapAesKeyAsync(byte[] plaintextKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plaintextKey);

        var (keyName, version) = ParseKeyId(_options.CurrentKeyId);
        var keyResponse = await _keyClient.GetKeyAsync(keyName, version, ct).ConfigureAwait(false);
        var cryptoClient = new CryptographyClient(keyResponse.Value.Id, _credential);

        var result = await cryptoClient.WrapKeyAsync(
            KeyWrapAlgorithm.RsaOaep256,
            plaintextKey,
            ct).ConfigureAwait(false);

        return Convert.ToBase64String(result.EncryptedKey);
    }

    /// <inheritdoc />
    public async Task<byte[]> UnwrapAesKeyAsync(string wrappedKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(wrappedKey))
        {
            throw new ArgumentException("包装密钥不可为空", nameof(wrappedKey));
        }

        var (keyName, version) = ParseKeyId(_options.CurrentKeyId);
        var keyResponse = await _keyClient.GetKeyAsync(keyName, version, ct).ConfigureAwait(false);
        var cryptoClient = new CryptographyClient(keyResponse.Value.Id, _credential);

        var wrappedBytes = Convert.FromBase64String(wrappedKey);
        var result = await cryptoClient.UnwrapKeyAsync(
            KeyWrapAlgorithm.RsaOaep256,
            wrappedBytes,
            ct).ConfigureAwait(false);

        return result.Key;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListKeyVersionsAsync(string keyName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyName))
        {
            throw new ArgumentException("密钥名称不可为空", nameof(keyName));
        }

        var versions = new List<string>();
        await foreach (var properties in _keyClient.GetPropertiesOfKeyVersionsAsync(keyName, ct).ConfigureAwait(false))
        {
            versions.Add(properties.Version ?? "unknown");
        }

        return versions;
    }

    private static (string name, string? version) ParseKeyId(string keyId)
    {
        // keyId 格式："key-v1" 或 "keyName/version" 或纯 keyName
        var slashIndex = keyId.IndexOf('/');
        if (slashIndex > 0)
        {
            return (keyId[..slashIndex], keyId[(slashIndex + 1)..]);
        }

        return (keyId, null);
    }

    /// <summary>
    /// RSA 适配器：将签名/解密操作委托给 Azure Key Vault CryptographyClient，
    /// 支持 HSM 不可导出密钥。
    /// </summary>
    private sealed class KeyVaultRsa : RSA
    {
        private readonly CryptographyClient _cryptoClient;
        private readonly RSAParameters _publicParams;
        private readonly int _keySize;

        public KeyVaultRsa(CryptographyClient cryptoClient, RSAParameters publicParams, int keySize)
        {
            _cryptoClient = cryptoClient;
            _publicParams = publicParams;
            _keySize = keySize;
        }

        public override int KeySize => _keySize;

        public override byte[] SignHash(byte[] hash, HashAlgorithmName hashAlgorithm, RSASignaturePadding padding)
        {
            var algorithm = MapSignatureAlgorithm(hashAlgorithm, padding);
            var result = _cryptoClient.Sign(algorithm, hash);
            return result.Signature;
        }

        public override bool VerifyHash(byte[] hash, byte[] signature, HashAlgorithmName hashAlgorithm, RSASignaturePadding padding)
        {
            var algorithm = MapSignatureAlgorithm(hashAlgorithm, padding);
            var result = _cryptoClient.Verify(algorithm, hash, signature);
            return result.IsValid;
        }

        public override byte[] Decrypt(byte[] data, RSAEncryptionPadding padding)
        {
            var algorithm = MapEncryptionAlgorithm(padding);
            var result = _cryptoClient.Decrypt(algorithm, data);
            return result.Plaintext;
        }

        public override byte[] Encrypt(byte[] data, RSAEncryptionPadding padding)
        {
            var algorithm = MapEncryptionAlgorithm(padding);
            var result = _cryptoClient.Encrypt(algorithm, data);
            return result.Ciphertext;
        }

        public override RSAParameters ExportParameters(bool includePrivateParameters)
        {
            if (includePrivateParameters)
            {
                throw new NotSupportedException("HSM 支持的密钥私钥不可导出");
            }

            return _publicParams;
        }

        public override void ImportParameters(RSAParameters parameters)
        {
            throw new NotSupportedException("Key Vault 托管密钥不可本地导入密钥参数");
        }

        // 注意：此处使用完全限定名 Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm，
        // 因为 KeyVaultRsa 继承自 RSA，而 .NET 10 的 RSA 基类含有实例属性 SignatureAlgorithm，
        // 简写 SignatureAlgorithm 会被解析为该实例属性导致编译失败。
        private static Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm MapSignatureAlgorithm(HashAlgorithmName hashAlgorithm, RSASignaturePadding padding)
        {
            var isPss = padding.Mode == RSASignaturePaddingMode.Pss;

            if (hashAlgorithm == HashAlgorithmName.SHA256)
                return isPss ? Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm.PS256 : Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm.RS256;
            if (hashAlgorithm == HashAlgorithmName.SHA384)
                return isPss ? Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm.PS384 : Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm.RS384;
            if (hashAlgorithm == HashAlgorithmName.SHA512)
                return isPss ? Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm.PS512 : Azure.Security.KeyVault.Keys.Cryptography.SignatureAlgorithm.RS512;

            throw new NotSupportedException($"不支持的哈希算法：{hashAlgorithm.Name}");
        }

        private static Azure.Security.KeyVault.Keys.Cryptography.EncryptionAlgorithm MapEncryptionAlgorithm(RSAEncryptionPadding padding)
        {
            // RSAEncryptionPadding.Mode 为 RSAEncryptionPaddingMode（Oaep / Pkcs1），
            // OaepHashAlgorithm 为 HashAlgorithmName；HashAlgorithmName 非常量类型，无法用于模式匹配，改用 if/else。
            if (padding.Mode == RSAEncryptionPaddingMode.Oaep)
            {
                if (padding.OaepHashAlgorithm == HashAlgorithmName.SHA256)
                    return Azure.Security.KeyVault.Keys.Cryptography.EncryptionAlgorithm.RsaOaep256;
                if (padding.OaepHashAlgorithm == HashAlgorithmName.SHA1)
                    return Azure.Security.KeyVault.Keys.Cryptography.EncryptionAlgorithm.RsaOaep;
            }

            throw new NotSupportedException($"不支持的加密填充方案：{padding}");
        }
    }
}
