using System.Security.Cryptography;
using Leno.UserAuth.Application.Abstractions;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// AES-256 加密服务，用于 OAuth2 ClientSecret 的加密存储。
/// 使用 CBC 模式 + PKCS7 填充，密钥通过构造函数注入。
/// </summary>
public sealed class AesEncryptionService : IClientSecretEncryptionService
{
    private readonly byte[] _key;

    /// <summary>
    /// 初始化 AES 加密服务。
    /// </summary>
    /// <param name="base64Key">Base64 编码的 32 字节（256 位）AES 密钥。</param>
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
    /// 加密明文，返回 Base64 编码的密文（含 IV 前缀）。
    /// 格式：Base64(IV[16 bytes] + Ciphertext)。
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new ArgumentException("明文不可为空", nameof(plainText));
        }

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // IV + Ciphertext
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// 解密密文（Base64 编码的 IV + Ciphertext），返回明文。
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            throw new ArgumentException("密文不可为空", nameof(cipherText));
        }

        var fullCipher = Convert.FromBase64String(cipherText);
        if (fullCipher.Length < 16)
        {
            throw new ArgumentException("密文长度不足", nameof(cipherText));
        }

        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = new byte[16];
        var cipherBytes = new byte[fullCipher.Length - 16];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);
        Buffer.BlockCopy(fullCipher, 16, cipherBytes, 0, cipherBytes.Length);

        using var decryptor = aes.CreateDecryptor(aes.Key, iv);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}