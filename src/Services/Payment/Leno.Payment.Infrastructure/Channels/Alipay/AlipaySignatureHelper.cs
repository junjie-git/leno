using System.Security.Cryptography;
using System.Text;

namespace Leno.Payment.Infrastructure.Channels.Alipay;

/// <summary>
/// 支付宝签名工具，生产环境使用 RSA-SHA256（RSA2）算法对参数排序拼接后签名。
/// 当前为占位实现：以 SHA256 作为签名占位（RSA 需真实证书与密钥），便于模拟联调。
/// 部署到生产环境前需替换为基于 RSA 私钥/公钥的真实签名与验签逻辑。
/// </summary>
public static class AlipaySignatureHelper
{
    /// <summary>
    /// 生成签名（占位）。生产环境应使用 RSA-SHA256 私钥对排序后的参数串签名。
    /// </summary>
    /// <param name="parameters">参与签名的参数（含 sign 字段会被自动排除）。</param>
    /// <param name="privateKey">占位密钥（生产环境为 RSA 私钥）。</param>
    public static string GenerateSign(Dictionary<string, string> parameters, string privateKey)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var sorted = parameters
            .Where(p => p.Key != "sign" && !string.IsNullOrEmpty(p.Value))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value}");
        var raw = string.Join("&", sorted) + privateKey;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 校验签名（占位）。生产环境应使用 RSA-SHA256 公钥验签。
    /// </summary>
    /// <param name="parameters">通知报文解析出的参数集合（含 sign）。</param>
    /// <param name="publicKey">占位密钥（生产环境为 RSA 公钥）。</param>
    /// <param name="sign">通知报文中携带的签名。</param>
    public static bool VerifySign(Dictionary<string, string> parameters, string publicKey, string? sign)
    {
        if (string.IsNullOrEmpty(sign))
        {
            return false;
        }

        var expected = GenerateSign(parameters, publicKey);
        return string.Equals(expected, sign, StringComparison.OrdinalIgnoreCase);
    }
}
