using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Leno.SharedKernel.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Infrastructure.Storage;

/// <summary>
/// 本地磁盘文件存储实现，按类别分目录存储，校验 URL 归属与文件大小。
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly LocalStorageOptions _options;
    private readonly ILogger<LocalFileStorageService> _logger;

    private static readonly HashSet<string> AllowedCategories =
        new(StringComparer.OrdinalIgnoreCase) { "avatar", "product", "review", "aftersales", "credential", "misc" };

    public LocalFileStorageService(IOptions<LocalStorageOptions> options, ILogger<LocalFileStorageService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new InvalidOperationException("LocalStorageOptions 未配置");
        _logger = logger;
    }

    public async Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string contentType, string category, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("文件名不可为空", nameof(fileName));
        }

        if (stream.CanSeek && stream.Length > _options.MaxFileSize)
        {
            throw new FileStorageException($"文件大小超过上限 {_options.MaxFileSize} 字节");
        }

        if (!string.IsNullOrEmpty(_options.BaseUrl) && !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _))
        {
            throw new FileStorageException("BaseUrl 配置非法");
        }

        var safeCategory = SanitizeCategory(category);
        var ext = Path.GetExtension(fileName);
        var storedName = $"{safeCategory}/{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_options.BasePath, storedName);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var fs = File.Create(fullPath))
        {
            await stream.CopyToAsync(fs, ct);
        }

        var size = stream.CanSeek ? stream.Length : new FileInfo(fullPath).Length;
        var url = BuildUrl(storedName);
        _logger.LogInformation("文件上传成功 Url={Url} Size={Size}", url, size);

        return new FileUploadResult(url, size, contentType);
    }

    public Task<Stream> DownloadAsync(string fileUrl, CancellationToken ct = default)
    {
        var relativePath = ResolveRelativePath(fileUrl);
        var fullPath = Path.Combine(_options.BasePath, relativePath);

        if (!File.Exists(fullPath))
        {
            throw new FileStorageException("文件不存在");
        }

        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        if (!ValidateUrlInternal(fileUrl))
        {
            return Task.CompletedTask;
        }

        var relativePath = ResolveRelativePath(fileUrl);
        var fullPath = Path.Combine(_options.BasePath, relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<string> GetUrlAsync(string fileName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var storedName = fileName.TrimStart('/');
        return Task.FromResult(BuildUrl(storedName));
    }

    public Task<bool> ValidateUrlAsync(string fileUrl, CancellationToken ct = default)
    {
        return Task.FromResult(ValidateUrlInternal(fileUrl));
    }

    public Task<bool> ExistsAsync(string fileUrl, CancellationToken ct = default)
    {
        if (!ValidateUrlInternal(fileUrl))
        {
            return Task.FromResult(false);
        }

        var relativePath = ResolveRelativePath(fileUrl);
        var fullPath = Path.Combine(_options.BasePath, relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    private bool ValidateUrlInternal(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return false;
        }

        return fileUrl.StartsWith(_options.BaseUrl + "/", StringComparison.Ordinal)
               || fileUrl.Equals(_options.BaseUrl, StringComparison.Ordinal);
    }

    private string ResolveRelativePath(string fileUrl)
    {
        if (!ValidateUrlInternal(fileUrl))
        {
            throw new FileStorageException("非法的文件 URL");
        }

        var prefix = _options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/";
        return fileUrl.StartsWith(prefix, StringComparison.Ordinal)
            ? fileUrl[prefix.Length..]
            : string.Empty;
    }

    private string BuildUrl(string storedName)
    {
        var baseWithSlash = _options.BaseUrl.EndsWith('/') ? _options.BaseUrl.TrimEnd('/') : _options.BaseUrl;
        return $"{baseWithSlash}/{storedName.TrimStart('/')}";
    }

    private static string SanitizeCategory(string? category)
    {
        return !string.IsNullOrWhiteSpace(category) && AllowedCategories.Contains(category)
            ? category.ToLowerInvariant()
            : "misc";
    }
}

/// <summary>
/// 文件存储领域异常，承载 HTTP 400 映射。
/// </summary>
internal sealed class FileStorageException : DomainException
{
    public FileStorageException(string message)
        : base(message, "FILE_STORAGE_ERROR", 400)
    {
    }
}
