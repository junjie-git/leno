using Leno.Infrastructure.Storage;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Infrastructure;

/// <summary>
/// 审计 3.11：图片上传仅校验扩展名，未校验文件内容/Magic Number。
/// 验证 FileSignatureDetector 正确识别 JPEG/PNG/WebP 真实文件，拒绝伪装扩展名的非图片文件。
/// </summary>
public sealed class FileSignatureDetectorTests
{
    private readonly FileSignatureDetector _detector = new();

    [Fact]
    public void IsValidImageSignature_Should_Return_True_For_Real_JPEG()
    {
        // JPEG Magic Number: FF D8 FF
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
        using var stream = new MemoryStream(jpegBytes);

        var result = _detector.IsValidImageSignature(stream, ".jpg");

        Assert.True(result);
    }

    [Fact]
    public void IsValidImageSignature_Should_Return_True_For_Real_JPEG_With_Jpeg_Extension()
    {
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 };
        using var stream = new MemoryStream(jpegBytes);

        var result = _detector.IsValidImageSignature(stream, ".jpeg");

        Assert.True(result);
    }

    [Fact]
    public void IsValidImageSignature_Should_Return_True_For_Real_PNG()
    {
        // PNG Magic Number: 89 50 4E 47 0D 0A 1A 0A
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        using var stream = new MemoryStream(pngBytes);

        var result = _detector.IsValidImageSignature(stream, ".png");

        Assert.True(result);
    }

    [Fact]
    public void IsValidImageSignature_Should_Return_True_For_Real_WebP()
    {
        // WebP Magic Number: 52 49 46 46 (RIFF) + 4 bytes size + 57 45 42 50 (WEBP)
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x1A, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        using var stream = new MemoryStream(webpBytes);

        var result = _detector.IsValidImageSignature(stream, ".webp");

        Assert.True(result);
    }

    [Fact]
    public void IsValidImageSignature_Should_Return_False_For_SVG_Disguised_As_JPG()
    {
        // SVG/HTML 内容伪装成 .jpg
        var svgBytes = System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert('xss')</script></svg>");
        using var stream = new MemoryStream(svgBytes);

        var result = _detector.IsValidImageSignature(stream, ".jpg");

        Assert.False(result);
    }

    [Fact]
    public void IsValidImageSignature_Should_Return_False_For_HTML_Disguised_As_PNG()
    {
        var htmlBytes = System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body><img src=x onerror=alert(1)></body></html>");
        using var stream = new MemoryStream(htmlBytes);

        var result = _detector.IsValidImageSignature(stream, ".png");

        Assert.False(result);
    }

    [Fact]
    public void IsValidImageSignature_Should_Return_False_For_Empty_Stream()
    {
        using var stream = new MemoryStream();

        var result = _detector.IsValidImageSignature(stream, ".jpg");

        Assert.False(result);
    }

    [Fact]
    public void IsValidImageSignature_Should_Return_False_For_Unknown_Extension()
    {
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF };
        using var stream = new MemoryStream(jpegBytes);

        var result = _detector.IsValidImageSignature(stream, ".gif");

        Assert.False(result);
    }

    [Fact]
    public void IsValidImageSignature_Should_Reset_Stream_Position_After_Check()
    {
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        using var stream = new MemoryStream(jpegBytes);
        stream.Position = 0;

        _detector.IsValidImageSignature(stream, ".jpg");

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void IsValidImageSignature_Should_Be_Case_Insensitive_For_Extension()
    {
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF };
        using var stream = new MemoryStream(jpegBytes);

        var result = _detector.IsValidImageSignature(stream, ".JPG");

        Assert.True(result);
    }

    [Fact]
    public void IsValidImageSignature_Should_Return_False_For_JPEG_Bytes_With_PNG_Extension()
    {
        // JPEG 内容但扩展名为 PNG，应拒绝
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        using var stream = new MemoryStream(jpegBytes);

        var result = _detector.IsValidImageSignature(stream, ".png");

        Assert.False(result);
    }
}
