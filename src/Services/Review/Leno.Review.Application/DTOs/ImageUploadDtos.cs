namespace Leno.Review.Application.DTOs;

/// <summary>
/// 图片上传结果 DTO。
/// </summary>
public sealed class ImageUploadResultDto
{
    public List<string> Urls { get; set; } = [];
}
