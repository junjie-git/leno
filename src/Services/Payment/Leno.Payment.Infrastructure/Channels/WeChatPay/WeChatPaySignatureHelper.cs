using System.Security.Cryptography;
using System.Text;

namespace Leno.Payment.Infrastructure.Channels.WeChatPay;

/// <summary>
/// 微信支付签名工具，基于 APIv2 HMAC-SHA256 算法。
/// 按参数名 ASCII 升序拼接（排除 sign 与空值），追加商户密钥后做 HMAC-SHA256，输出大写十六进制。
/// </summary>
public static class WeChatPaySignatureHelper
{
    /// <summary>
    /// 生成签名。
    /// </summary>
    /// <param name="parameters">参与签名的参数（含 sign 字段会被自动排除）。</param>
    /// <param name="apiKey">商户 API 密钥。</param>
    public static string GenerateSign(Dictionary<string, string> parameters, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentException("微信支付 API 密钥不可为空", nameof(apiKey));
        }

        var sorted = parameters
            .Where(p => p.Key != "sign" && !string.IsNullOrEmpty(p.Value))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value}");
        var raw = string.Join("&", sorted) + "&key=" + apiKey;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 校验签名：用相同算法重算并与传入 sign 比较（大小写不敏感）。
    /// </summary>
    /// <param name="parameters">通知报文解析出的参数集合（含 sign）。</param>
    /// <param name="apiKey">商户 API 密钥。</param>
    /// <param name="sign">通知报文中携带的签名。</param>
    public static bool VerifySign(Dictionary<string, string> parameters, string apiKey, string? sign)
    {
        if (string.IsNullOrEmpty(sign))
        {
            return false;
        }

        var expected = GenerateSign(parameters, apiKey);
        return string.Equals(expected, sign, StringComparison.OrdinalIgnoreCase);
    }
}
