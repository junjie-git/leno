namespace Leno.UserAuth.Application.Abstractions;

/// <summary>
/// OAuth2 ClientSecret 加密服务抽象，定义在应用层，由基础设施层实现。
/// </summary>
public interface IClientSecretEncryptionService
{
    /// <summary>加密明文 ClientSecret，返回密文。</summary>
    string Encrypt(string plainText);

    /// <summary>解密密文 ClientSecret，返回明文。</summary>
    string Decrypt(string cipherText);
}