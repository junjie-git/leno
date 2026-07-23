using Leno.Identity.Domain.Services;
using OtpNet;

namespace Leno.Identity.Infrastructure.Services;

/// <summary>
/// TOTP 令牌验证器实现（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// 基于 Otp.NET 库，使用 SHA-1 算法、30 秒时间窗口、6 位验证码。
/// 从 UserAuth BC 的 TotpTokenVerifier 迁入，逻辑保持一致。
/// </summary>
public sealed class TotpTokenVerifier : ITokenVerifier
{
    /// <summary>默认发行方名称。</summary>
    private const string DefaultIssuer = "Leno";

    /// <inheritdoc />
    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    /// <inheritdoc />
    public string GenerateQrCodeUri(string accountName, string secret, string issuer = DefaultIssuer)
    {
        // 注意：Base32Encoding.ToBytes 会校验 secret 合法性；非法 secret 抛异常由调用方处理
        var key = Base32Encoding.ToBytes(secret);
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountName);

        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    /// <inheritdoc />
    public bool Verify(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        try
        {
            var key = Base32Encoding.ToBytes(secret.Trim());
            var totp = new Totp(key);
            return totp.VerifyTotp(code.Trim(), out _, window: new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            // 非法 Base32 secret 或其他异常时统一返回 false，不向上抛
            return false;
        }
    }
}
