namespace Leno.Infrastructure.Security;

/// <summary>
/// JWT 签名配置（3.10 安全技术栈升级），对应 appsettings.json 中 <c>JwtSigning</c> 节。
/// </summary>
public sealed class JwtSigningOptions
{
    /// <summary>签名模式：Hs256（对称，默认）/ Dual（过渡）/ Rs256（非对称目标态）。</summary>
    public string SigningMode { get; set; } = "Hs256";

    /// <summary>当前 RSA 密钥标识（版本化，如 "key-v1"），用于 KMS 密钥路由。</summary>
    public string CurrentKeyId { get; set; } = "key-v1";

    /// <summary>访问令牌 TTL（分钟）。</summary>
    public int TokenTtlMinutes { get; set; } = 30;

    /// <summary>HS256 对称签名密钥（UTF-8 编码至少 32 字节）。Dual/Hs256 模式必需。</summary>
    public string Hs256SigningKey { get; set; } = string.Empty;

    /// <summary>JWT 发行方标识（与 Identity:Jwt:Issuer 一致）。</summary>
    public string Issuer { get; set; } = "leno-identity";

    /// <summary>JWT 受众标识（与 Identity:Jwt:Audience 一致）。</summary>
    public string Audience { get; set; } = "leno-clients";

    /// <summary>RSA 私钥 PEM 字符串（EnvironmentKms 回退用，生产环境应通过 KMS 获取）。</summary>
    public string RsaPrivateKeyPem { get; set; } = string.Empty;

    /// <summary>RSA 公钥 PEM 字符串（验签用）。</summary>
    public string RsaPublicKeyPem { get; set; } = string.Empty;

    /// <summary>是否使用 Azure Key Vault 作为 KMS 后端（false 时使用 EnvironmentKms）。</summary>
    public bool UseAzureKeyVault { get; set; } = false;

    /// <summary>Azure Key Vault URI（当 <see cref="UseAzureKeyVault"/> 为 true 时必需）。</summary>
    public string KeyVaultUri { get; set; } = string.Empty;

    /// <summary>配置节名称。</summary>
    public const string SectionName = "JwtSigning";
}
