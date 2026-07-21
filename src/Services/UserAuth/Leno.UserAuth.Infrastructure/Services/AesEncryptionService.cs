using System.Security.Cryptography;
using Leno.UserAuth.Application.Abstractions;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// AES-GCM 加密服务，用于 OAuth2 ClientSecret 的加密存储。
/// 格式：Base64(Nonce[12B] + Ciphertext + Tag[16B])，提供认证加密，防止 Padding Oracle 与密文篡改。
/// </summary>
public sealed class AesEncryptionService : IClientSecretEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesEncryptionService(string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            throw new ArgumentException("AES 密钥不可为空", nameof(base64Key));
        }

        _key = Convert.FromBase64String(base64Key);
        if (_key.Length != 32)
        {
            throw new ArgumentException("AES 密钥必须为 32 字节（256 位）", nameof(base64Key));
        }
    }

    /// <summary>
    /// 加密明文，返回 Base64 编码的密文（含 Nonce 前缀与 Tag 后缀）。
    /// 格式：Base64(Nonce[12B] + Ciphertext + Tag[16B])。
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new ArgumentException("明文不可为空", nameof(plainText));
        }

        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
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

    /// <summary>
    /// 解密密文（Base64 编码的 Nonce + Ciphertext + Tag），返回明文。
    /// Tag 校验失败抛 <see cref="CryptographicException"/>，防止密文篡改。
    /// </summary>
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

        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}
