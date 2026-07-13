namespace Leno.Infrastructure.Abstractions;

/// <summary>
/// 文件上传结果。
/// </summary>
public sealed record FileUploadResult(string Url, long Size, string ContentType);

/// <summary>
/// 文件存储服务抽象，支持本地磁盘与对象存储（MinIO/OSS）适配。
/// 领域层依赖此抽象，基础设施层提供实现。
/// </summary>
public interface IFileStorageService
{
    Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string contentType, string category, CancellationToken ct = default);

    Task<Stream> DownloadAsync(string fileUrl, CancellationToken ct = default);

    Task DeleteAsync(string fileUrl, CancellationToken ct = default);

    Task<string> GetUrlAsync(string fileName, CancellationToken ct = default);

    Task<bool> ValidateUrlAsync(string fileUrl, CancellationToken ct = default);

    Task<bool> ExistsAsync(string fileUrl, CancellationToken ct = default);
}
