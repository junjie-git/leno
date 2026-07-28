namespace Leno.Infrastructure.Abstractions.UserAgent;

/// <summary>
/// User-Agent 解析抽象：从 UA 字符串解析浏览器、操作系统、设备指纹。
/// 实现位于 Leno.Infrastructure（UAParserUserAgentParser）。
/// </summary>
public interface IUserAgentParser
{
    string ParseBrowser(string userAgent);
    string ParseOs(string userAgent);
    string? ParseDeviceFingerprint(string userAgent);
}
