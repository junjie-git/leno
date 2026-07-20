using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Infrastructure.Channels.Alipay;

/// <summary>
/// 支付宝签名工具，使用 RSA-SHA256（RSA2）算法对排序后的参数串签名与验签。
/// </summary>
public static class AlipaySignatureHelper
{
    /// <summary>
    /// 生成签名。使用 RSA-SHA256（RSA2）私钥对排序后的参数串签名，返回 Base64 字符串。
    /// </summary>
    /// <param name="parameters">参与签名的参数（含 sign 字段会被自动排除，空值项会被排除）。</param>
    /// <param name="privateKey">PEM 格式的 RSA 私钥字符串。</param>
    public static string GenerateSign(Dictionary<string, string> parameters, string privateKey)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException("支付宝 RSA 私钥不可为空", nameof(privateKey));
        }

        var content = BuildSignContent(parameters);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(content),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// 校验签名。使用 RSA-SHA256（RSA2）公钥对排序后的参数串验签。
    /// </summary>
    /// <param name="parameters">通知报文解析出的参数集合（含 sign，验签时自动排除）。</param>
    /// <param name="publicKey">PEM 格式的 RSA 公钥字符串。</param>
    /// <param name="sign">通知报文中携带的签名。</param>
    /// <param name="logger">可选日志记录器，传入时按异常类型分类记日志。</param>
    public static bool VerifySign(
        Dictionary<string, string> parameters,
        string publicKey,
        string? sign,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(sign))
        {
            return false;
        }

        try
        {
            var content = BuildSignContent(parameters);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKey);
            return rsa.VerifyData(
                Encoding.UTF8.GetBytes(content),
                Convert.FromBase64String(sign),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (ArgumentException ex) when (ex is not ArgumentNullException)
        {
            // 公钥 PEM 格式错误（无 PEM 标记、label 非法等）：配置问题，需立即修复。
            // 排除 ArgumentNullException：编程错误（如 parameters=null）应冒泡到调用方 fail-fast。
            logger?.LogError(ex, "支付宝公钥 PEM 格式错误，验签失败");
            return false;
        }
        catch (FormatException ex)
        {
            // sign 非 Base64：可能是攻击或客户端异常
            logger?.LogWarning(ex, "支付宝 sign 字段非合法 Base64");
            return false;
        }
        catch (CryptographicException ex)
        {
            // RSA 验签失败：PEM ASN.1 内容非法或签名不匹配
            logger?.LogDebug(ex, "支付宝 RSA 验签失败（签名不匹配或公钥内容非法）");
            return false;
        }
        // 不再吞其他异常：未知异常冒泡由调用方处理
    }

    /// <summary>
    /// 构建待签名内容：排除 sign 字段与空值项，按 key 的 ASCII 升序排序，拼接为 key1=value1&amp;key2=value2...。
    /// </summary>
    /// <param name="parameters">参与签名的参数集合。</param>
    private static string BuildSignContent(Dictionary<string, string> parameters)
    {
        var sorted = parameters
            .Where(p => p.Key != "sign" && !string.IsNullOrEmpty(p.Value))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value}");
        return string.Join("&", sorted);
    }
}
