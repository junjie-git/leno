using System.Security.Cryptography;
using System.Text;
using Leno.Infrastructure.Abstractions.UserAgent;
using UAParser;

namespace Leno.Infrastructure.UserAgent;

/// <summary>
/// UA Parser NuGet 包封装：解析浏览器、操作系统、设备指纹。
/// 设备指纹 = SHA256(UA 字符串前 8 位)。
/// </summary>
public sealed class UAParserUserAgentParser : IUserAgentParser
{
    private static readonly Parser UAEngine = Parser.GetDefault();
    private const int FingerprintLength = 8;

    /// <inheritdoc />
    public string ParseBrowser(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown";
        try
        {
            var clientInfo = UAEngine.Parse(userAgent);
            var family = clientInfo.UA.Family ?? "Unknown";
            var major = clientInfo.UA.Major;
            return string.IsNullOrEmpty(major) ? family : $"{family} {major}";
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <inheritdoc />
    public string ParseOs(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown";
        try
        {
            var clientInfo = UAEngine.Parse(userAgent);
            var family = clientInfo.OS.Family ?? "Unknown";
            var major = clientInfo.OS.Major;
            return string.IsNullOrEmpty(major) ? family : $"{family} {major}";
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <inheritdoc />
    public string? ParseDeviceFingerprint(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        try
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(userAgent));
            var sb = new StringBuilder(FingerprintLength);
            for (int i = 0; i < FingerprintLength / 2 && i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
        catch
        {
            return null;
        }
    }
}
