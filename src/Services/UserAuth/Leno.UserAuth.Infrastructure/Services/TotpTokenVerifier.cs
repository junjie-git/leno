using Leno.UserAuth.Domain.Services;
using OtpNet;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// TOTP 令牌验证器实现，基于 Otp.NET 库。
/// 使用 SHA-1 算法，30 秒时间窗口，6 位验证码。
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
        var key = Base32Encoding.ToBytes(secret);
        var totp = new Totp(key);
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
            long timeStepMatched;
            return totp.VerifyTotp(code.Trim(), out timeStepMatched, window: new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }
}