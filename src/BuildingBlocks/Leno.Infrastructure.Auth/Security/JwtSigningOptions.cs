namespace Leno.Infrastructure.Security;

/// <summary>
/// JWT 签名配置（3.10 安全技术栈升级），对应 appsettings.json 中 <c>JwtSigning</c> 节。
/// <para>
/// 双轨下线 A6（2026-09-23，D-6 单算法）：<b>RS256-only</b> ——
/// 原 Hs256/Dual 过渡模式与 <c>SigningMode</c>/<c>Hs256SigningKey</c> 配置已删除；
/// RSA 私钥经 <see cref="RsaPrivateKeyPem"/>（开发回退）或 Azure Key Vault（生产）获取。
/// </para>
/// </summary>
public sealed class JwtSigningOptions
{
    /// <summary>当前 RSA 密钥标识（版本化，如 "key-v1"），用于 KMS 密钥路由与 JWT kid 头。</summary>
    public string CurrentKeyId { get; set; } = "key-v1";

    /// <summary>
    /// 轮换重叠期需一并发布到 JWKS 的上一把（或多把）公钥标识，逗号分隔（如 "key-v1"）。
    /// <para>
    /// 密钥轮换四步法（见 deploy/docs/jwt-key-rotation-runbook.md）的第 2 步要求 JWKS 同时发布
    /// 新旧公钥，使持有旧令牌的客户端在缓存刷新完成前仍可验签；重叠窗口结束后移除旧 KeyId。
    /// </para>
    /// </summary>
    public string PreviousKeyIds { get; set; } = string.Empty;

    /// <summary>访问令牌 TTL（分钟）。</summary>
    public int TokenTtlMinutes { get; set; } = 30;

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
