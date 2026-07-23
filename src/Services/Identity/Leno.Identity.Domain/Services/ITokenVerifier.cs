namespace Leno.Identity.Domain.Services;

/// <summary>
/// TOTP 令牌验证器抽象，封装 TOTP 令牌生成与验证细节。
/// 实现位于基础设施层（基于 Otp.NET），领域层不直接依赖第三方库。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public interface ITokenVerifier
{
    /// <summary>生成 TOTP 共享密钥（Base32）。</summary>
    string GenerateSecret();

    /// <summary>生成 TOTP 认证器 URI，用于生成 QR 码。</summary>
    /// <param name="accountName">账户标识（如用户名或邮箱）。</param>
    /// <param name="secret">Base32 密钥。</param>
    /// <param name="issuer">发行方名称（如 "Leno"）。</param>
    string GenerateQrCodeUri(string accountName, string secret, string issuer = "Leno");

    /// <summary>验证 TOTP 码是否有效。</summary>
    /// <param name="secret">Base32 密钥。</param>
    /// <param name="code">用户输入的 6 位 TOTP 码。</param>
    bool Verify(string secret, string code);
}
