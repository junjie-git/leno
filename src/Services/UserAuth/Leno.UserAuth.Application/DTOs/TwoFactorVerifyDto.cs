namespace Leno.UserAuth.Application.DTOs;

/// <summary>
/// 双因子认证二次验证请求 DTO（登录流程）。</summary>
public sealed class TwoFactorVerifyDto
{
    /// <summary>第一步登录返回的临时令牌。</summary>
    public string TempToken { get; init; } = string.Empty;

    /// <summary>6 位 TOTP 验证码。</summary>
    public string Code { get; init; } = string.Empty;
}