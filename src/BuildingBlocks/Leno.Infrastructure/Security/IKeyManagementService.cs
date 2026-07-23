using System.Security.Cryptography;

namespace Leno.Infrastructure.Security;

/// <summary>
/// 密钥管理服务抽象（3.10 安全技术栈升级 / KMS 托管）。
/// <para>
/// 统一封装 RSA 签名密钥的获取与 AES 密钥的包装/解包，屏蔽具体 KMS 后端（环境变量 / Azure Key Vault）。
/// 实现：<see cref="EnvironmentKms"/>（开发/回退）、<see cref="AzureKeyVaultKms"/>（生产）。
/// </para>
/// </summary>
public interface IKeyManagementService
{
    /// <summary>获取指定 KeyId 的 RSA 私钥实例（用于签名/解密）。</summary>
    /// <param name="keyId">密钥标识（版本化，如 "key-v1"）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>可执行私钥操作的 RSA 实例。</returns>
    Task<RSA> GetPrivateKeyAsync(string keyId, CancellationToken ct);

    /// <summary>获取指定 KeyId 的 RSA 公钥实例（用于验签/加密）。</summary>
    /// <param name="keyId">密钥标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>仅含公钥的 RSA 实例。</returns>
    Task<RSA> GetPublicKeyAsync(string keyId, CancellationToken ct);

    /// <summary>使用 KMS 主密钥包装（加密）AES 明文密钥。</summary>
    /// <param name="plaintextKey">AES 明文密钥字节。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>Base64 编码的包装后密钥。</returns>
    Task<string> WrapAesKeyAsync(byte[] plaintextKey, CancellationToken ct);

    /// <summary>使用 KMS 主密钥解包（解密）AES 包装密钥。</summary>
    /// <param name="wrappedKey">Base64 编码的包装密钥。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>AES 明文密钥字节。</returns>
    Task<byte[]> UnwrapAesKeyAsync(string wrappedKey, CancellationToken ct);

    /// <summary>列出指定密钥名称的所有可用版本（如 ["key-v1", "key-v2"]）。</summary>
    /// <param name="keyName">密钥名称（不含版本后缀）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>版本标识列表。</returns>
    Task<IReadOnlyList<string>> ListKeyVersionsAsync(string keyName, CancellationToken ct);
}
