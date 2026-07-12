using System.Security.Cryptography;
using System.Text;

namespace Leno.Payment.Infrastructure.Channels.WeChatPay;

/// <summary>
/// 微信支付 APIv3 签名工具，基于 RSA-SHA256 算法。
/// 用于生成请求 Authorization 头以及验证回调通知签名。
/// </summary>
public static class WeChatPayV3SignatureHelper
{
    private const string AuthorizationFormat = "WECHATPAY2-SHA256-RSA2048 mchid=\"{0}\",nonce_str=\"{1}\",signature=\"{2}\",timestamp=\"{3}\",serial_no=\"{4}\"";

    /// <summary>
    /// 生成请求签名并返回 Authorization 头的值。
    /// </summary>
    /// <param name="httpMethod">HTTP 方法（GET/POST 等）。</param>
    /// <param name="urlPath">请求 URL 路径部分（不含 query string）。</param>
    /// <param name="body">请求体 JSON 字符串，GET 请求传空字符串。</param>
    /// <param name="timestamp">Unix 时间戳（秒）。</param>
    /// <param name="nonce">随机字符串。</param>
    /// <param name="privateKey">商户 API 私钥（PEM 格式）。</param>
    /// <param name="mchId">商户号。</param>
    /// <param name="serialNo">商户证书序列号。</param>
    public static string GenerateAuthorization(
        string httpMethod,
        string urlPath,
        string body,
        string timestamp,
        string nonce,
        string privateKey,
        string mchId,
        string serialNo)
    {
        var message = BuildMessage(httpMethod, urlPath, timestamp, nonce, body);
        var signature = Sign(message, privateKey);
        return string.Format(AuthorizationFormat, mchId, nonce, signature, timestamp, serialNo);
    }

    /// <summary>
    /// 使用 RSA-SHA256 对消息进行签名，返回 Base64 编码的签名。
    /// </summary>
    /// <param name="message">待签名的消息。</param>
    /// <param name="privateKey">商户 API 私钥（PEM 格式）。</param>
    public static string Sign(string message, string privateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);
        var data = Encoding.UTF8.GetBytes(message);
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// 验证回调通知签名。
    /// </summary>
    /// <param name="timestamp">回调头 Wechatpay-Timestamp。</param>
    /// <param name="nonce">回调头 Wechatpay-Nonce。</param>
    /// <param name="body">回调请求体原始 JSON。</param>
    /// <param name="signature">回调头 Wechatpay-Signature（Base64）。</param>
    /// <param name="publicKey">微信支付平台公钥（PEM 格式）。</param>
    public static bool VerifyNotifySign(
        string timestamp,
        string nonce,
        string body,
        string signature,
        string publicKey)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(publicKey))
        {
            return false;
        }

        try
        {
            var message = BuildCallbackMessage(timestamp, nonce, body);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKey);
            var data = Encoding.UTF8.GetBytes(message);
            var signatureBytes = Convert.FromBase64String(signature);
            return rsa.VerifyData(data, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 读取私钥文件内容。
    /// </summary>
    /// <param name="privateKeyPath">私钥文件路径。</param>
    public static string LoadPrivateKeyFromFile(string privateKeyPath)
    {
        if (string.IsNullOrEmpty(privateKeyPath))
        {
            throw new ArgumentException("私钥文件路径不可为空", nameof(privateKeyPath));
        }

        return File.ReadAllText(privateKeyPath);
    }

    private static string BuildMessage(string httpMethod, string urlPath, string timestamp, string nonce, string body)
    {
        return $"{httpMethod}\n{urlPath}\n{timestamp}\n{nonce}\n{body}\n";
    }

    private static string BuildCallbackMessage(string timestamp, string nonce, string body)
    {
        return $"{timestamp}\n{nonce}\n{body}\n";
    }
}