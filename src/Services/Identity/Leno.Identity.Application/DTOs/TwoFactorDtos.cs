namespace Leno.Identity.Application.DTOs;

/// <summary>
/// 双因子认证验证请求 DTO（Identity BC）。
/// 用于登录流程二次验证，提交 TOTP 验证码。
/// </summary>
public sealed class TwoFactorVerifyDto
{
    /// <summary>用户输入的 6 位 TOTP 验证码。</summary>
    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// 双因子认证确认请求 DTO（Identity BC）。
/// 用于启用 2FA 后的首次确认验证。
/// </summary>
public sealed class TwoFactorConfirmDto
{
    /// <summary>用户输入的 6 位 TOTP 验证码。</summary>
    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// 启用双因子认证响应 DTO（Identity BC）。
/// 返回 TOTP 共享密钥与 QR 码 URI，供客户端生成二维码。
/// </summary>
public sealed class TwoFactorEnableResponseDto
{
    /// <summary>Base32 编码的 TOTP 共享密钥。</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>OTP Authenticator URI（otpauth://），前端据此生成 QR 码。</summary>
    public string QrCodeUri { get; init; } = string.Empty;
}
