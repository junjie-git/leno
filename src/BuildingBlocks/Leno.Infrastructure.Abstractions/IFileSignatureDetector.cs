namespace Leno.Infrastructure.Abstractions;

/// <summary>
/// 文件签名（Magic Number）校验器，读取文件头部字节匹配图片格式签名。
/// 防止伪装扩展名上传非图片文件（如 .jpg 扩展名的 SVG/HTML），避免存储型 XSS 与内容嗅探攻击。
/// 审计 3.11：图片上传仅校验扩展名，未校验文件内容/Magic Number。
/// </summary>
public interface IFileSignatureDetector
{
    /// <summary>
    /// 校验文件流头部 Magic Number 是否与扩展名预期的图片格式匹配。
    /// 校验后会将流 Position 重置为 0（当流支持 Seek 时），保证后续上传读取完整内容。
    /// </summary>
    /// <param name="stream">文件流，须可读。</param>
    /// <param name="extension">文件扩展名（含点，如 .jpg/.png/.webp），大小写不敏感。</param>
    /// <returns>匹配返回 true，不匹配或无法读取返回 false。</returns>
    bool IsValidImageSignature(Stream stream, string extension);
}
