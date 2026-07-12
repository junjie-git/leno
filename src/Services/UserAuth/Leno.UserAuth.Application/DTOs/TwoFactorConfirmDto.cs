namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 双因子认证确认请求 DTO。
/// </summary>
public sealed class TwoFactorConfirmDto
{
    /// <summary>6 位 TOTP 验证码。</summary>
    public string Code { get; init; } = string.Empty;
}