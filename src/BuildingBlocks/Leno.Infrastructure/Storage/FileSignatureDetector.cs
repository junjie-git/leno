using Leno.Infrastructure.Abstractions;

namespace Leno.Infrastructure.Storage;

/// <summary>
/// 文件签名（Magic Number）校验器实现。
/// 读取文件头部最多 12 字节，匹配 JPEG/PNG/WebP 三种图片格式的 Magic Number。
/// 校验后将流 Position 重置为 0（当流支持 Seek 时），保证后续上传读取完整内容。
/// 审计 3.11：图片上传仅校验扩展名，未校验文件内容/Magic Number。
/// </summary>
public sealed class FileSignatureDetector : IFileSignatureDetector
{
    /// <summary>
    /// 各扩展名对应的 Magic Number 列表（多组签名时任一匹配即通过）。
    /// JPEG: FF D8 FF（3 字节）
    /// PNG: 89 50 4E 47 0D 0A 1A 0A（8 字节）
    /// WebP: 52 49 46 46 ?? ?? ?? ?? 57 45 42 50（RIFF....WEBP，12 字节）
    /// </summary>
    private static readonly Dictionary<string, byte[][]> ImageSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
        [".png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        // WebP: RIFF????WEBP，第 0-3 字节固定 RIFF，第 8-11 字节固定 WEBP，中间 4 字节为文件大小（可变）
        [".webp"] = [[0x52, 0x49, 0x46, 0x46, 0x57, 0x45, 0x42, 0x50]]
    };

    /// <summary>
    /// 需读取的最大头部字节数（WebP 需要 12 字节：RIFF(4) + size(4) + WEBP(4)）。
    /// </summary>
    private const int MaxHeaderBytes = 12;

    /// <inheritdoc />
    public bool IsValidImageSignature(Stream stream, string extension)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        if (!ImageSignatures.TryGetValue(extension, out var signatures))
        {
            return false;
        }

        if (!stream.CanRead)
        {
            return false;
        }

        // 记录原始 Position，校验后重置
        var originalPosition = stream.CanSeek ? stream.Position : -1;

        try
        {
            var buffer = new byte[MaxHeaderBytes];
            var bytesRead = stream.Read(buffer, 0, MaxHeaderBytes);

            if (bytesRead == 0)
            {
                return false;
            }

            foreach (var signature in signatures)
            {
                if (MatchesSignature(buffer, bytesRead, signature))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            // 重置流 Position，保证后续上传读取完整内容
            if (stream.CanSeek && originalPosition >= 0)
            {
                stream.Position = originalPosition;
            }
        }
    }

    /// <summary>
    /// 校验 buffer 前 signature.Length 字节是否与 signature 完全匹配。
    /// WebP 签名跳过 RIFF 与 WEBP 之间的 4 字节大小字段。
    /// </summary>
    private static bool MatchesSignature(byte[] buffer, int bytesRead, byte[] signature)
    {
        if (bytesRead < signature.Length)
        {
            return false;
        }

        // WebP 特殊处理：签名为 RIFF????WEBP，校验第 0-3 字节与第 8-11 字节（跳过 4-7 字节的大小字段）
        // 签名数组中存储的是 [R, I, F, F, W, E, B, P]，需拆分校验
        if (signature.Length == 8 && signature[0] == 0x52 && signature[1] == 0x49 && signature[2] == 0x46 && signature[3] == 0x46)
        {
            // 校验 RIFF（前 4 字节）
            for (var i = 0; i < 4; i++)
            {
                if (buffer[i] != signature[i])
                {
                    return false;
                }
            }

            // 校验 WEBP（第 8-11 字节），需要至少读取 12 字节
            if (bytesRead < 12)
            {
                return false;
            }

            for (var i = 4; i < 8; i++)
            {
                if (buffer[i + 4] != signature[i])
                {
                    return false;
                }
            }

            return true;
        }

        // 通用校验：前 signature.Length 字节逐一匹配
        for (var i = 0; i < signature.Length; i++)
        {
            if (buffer[i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }
}
