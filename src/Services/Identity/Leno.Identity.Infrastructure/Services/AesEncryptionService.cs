using System.Security.Cryptography;
using System.Text;
using Leno.Identity.Application.Abstractions;

namespace Leno.Identity.Infrastructure.Services;

/// <summary>
/// AES-GCM 加密服务实现（Identity BC），用于 OAuth2 ClientSecret 的加密存储。
/// <para>
/// 格式：Base64(Nonce[12B] + Ciphertext + Tag[16B])，提供认证加密，防止 Padding Oracle 与密文篡改。
/// 密钥由 <c>OAuth2:AesKey</c> 配置提供（Base64 编码的 32 字节 / 256 位密钥）。
/// </para>
/// <para>
/// 启动期 fail-fast：<see cref="AesEncryptionService(string)"/> 校验密钥长度与格式，
/// 缺失或长度不符时直接抛异常，避免运行时静默跳过加密导致 OAuthClientAppService 写入明文 ClientSecret。
/// </para>
/// 从 UserAuth BC 同名实现迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public sealed class AesEncryptionService : IClientSecretEncryptionService
{
    /// <summary>AES-GCM Nonce 长度（12 字节，NIST 推荐）。</summary>
    private const int NonceSize = 12;

    /// <summary>AES-GCM Tag 长度（16 字节，128 位认证标签）。</summary>
    private const int TagSize = 16;

    /// <summary>AES-256 密钥长度（32 字节）。</summary>
    private const int KeySizeBytes = 32;

    private readonly byte[] _key;

    /// <summary>
    /// 初始化 <see cref="AesEncryptionService"/> 的新实例。
    /// </summary>
    /// <param name="base64Key">Base64 编码的 AES-256 密钥（32 字节）。</param>
    public AesEncryptionService(string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            throw new ArgumentException("AES 密钥不可为空", nameof(base64Key));
        }

        _key = Convert.FromBase64String(base64Key);
        if (_key.Length != KeySizeBytes)
        {
            throw new ArgumentException(
                $"AES 密钥必须为 {KeySizeBytes} 字节（256 位），当前为 {_key.Length} 字节。",
                nameof(base64Key));
        }
    }

    /// <inheritdoc />
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new ArgumentException("明文不可为空", nameof(plainText));
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var result = new byte[NonceSize + cipherBytes.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSize, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + cipherBytes.Length, TagSize);

        return Convert.ToBase64String(result);
    }

    /// <inheritdoc />
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            throw new ArgumentException("密文不可为空", nameof(cipherText));
        }

        var fullCipher = Convert.FromBase64String(cipherText);
        if (fullCipher.Length < NonceSize + TagSize)
        {
            throw new ArgumentException("密文长度不足", nameof(cipherText));
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipherBytes = new byte[fullCipher.Length - NonceSize - TagSize];

        Buffer.BlockCopy(fullCipher, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(fullCipher, NonceSize, cipherBytes, 0, cipherBytes.Length);
        Buffer.BlockCopy(fullCipher, NonceSize + cipherBytes.Length, tag, 0, TagSize);

        var plainBytes = new byte[cipherBytes.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
