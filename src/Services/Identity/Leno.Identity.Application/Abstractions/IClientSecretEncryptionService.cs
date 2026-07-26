namespace Leno.Identity.Application.Abstractions;

/// <summary>
/// OAuth2 客户端密钥加密服务抽象（Identity BC）。
/// OAuthClient 聚合的 ClientSecret 字段以 AES-256 加密存储，查询时掩码返回。
/// 应用层通过本抽象完成明文 → 密文的加密转换，不感知底层密钥管理实现。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public interface IClientSecretEncryptionService
{
    /// <summary>
    /// 将明文 ClientSecret 加密为密文。
    /// </summary>
    /// <param name="plainSecret">第三方平台分配的明文密钥。</param>
    /// <returns>AES-256 加密后的密文。</returns>
    string Encrypt(string plainSecret);

    /// <summary>
    /// 将密文 ClientSecret 解密为明文。
    /// </summary>
    /// <param name="cipherSecret">已加密的密文。</param>
    /// <returns>解密后的明文密钥。</returns>
    string Decrypt(string cipherSecret);
}
