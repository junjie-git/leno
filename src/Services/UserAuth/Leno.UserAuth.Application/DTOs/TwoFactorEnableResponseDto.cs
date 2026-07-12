namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 双因子认证启用响应 DTO。
/// </summary>
public sealed class TwoFactorEnableResponseDto
{
    /// <summary>Base32 共享密钥。</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>TOTP 认证器 URI，用于生成 QR 码。</summary>
    public string QrCodeUri { get; init; } = string.Empty;
}