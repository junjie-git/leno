using Leno.SharedKernel.Abstractions;
using Leno.Infrastructure.Abstractions;
using Leno.SharedKernel.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Leno.Infrastructure.Storage;

/// <summary>
/// 基于 MinIO 的对象存储实现，适配 <see cref="IFileStorageService"/>。
/// 支持文件上传、下载、删除、URL 校验与存在性检查。
/// 敏感参数（AccessKey/SecretKey）通过环境变量或配置中心读取，不硬编码。
/// </summary>
public sealed class ObjectStorageService : IFileStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ObjectStorageOptions _options;
    private readonly ILogger<ObjectStorageService> _logger;

    /// <summary>双重检查锁定用的同步对象，保证 EnsureBucketExistsOnceAsync 串行执行。</summary>
    private readonly SemaphoreSlim _bucketEnsureLock = new(1, 1);

    /// <summary>0=未确保，1=已确保。使用 Volatile.Read/Write 保证跨线程可见性。</summary>
    private int _bucketEnsured;

    /// <summary>
    /// 指示 Bucket 确保操作是否尚未执行（延迟到首次使用）。
    /// 测试可据此断言构造函数未执行 sync-over-async。
    /// </summary>
    internal bool IsBucketEnsurePending => Volatile.Read(ref _bucketEnsured) == 0;

    private static readonly HashSet<string> AllowedCategories =
        new(StringComparer.OrdinalIgnoreCase) { "avatar", "product", "review", "aftersales", "credential", "misc" };

    /// <summary>
    /// 初始化 MinIO 对象存储服务。
    /// </summary>
    /// <param name="options">对象存储配置选项。</param>
    /// <param name="logger">日志记录器。</param>
    public ObjectStorageService(IOptions<ObjectStorageOptions> options, ILogger<ObjectStorageService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new InvalidOperationException("ObjectStorageOptions 未配置");
        _logger = logger;

        // 敏感参数优先从环境变量读取，避免硬编码在配置文件中
        var accessKey = ResolveSensitiveValue(_options.AccessKey, "FILE_STORAGE_ACCESS_KEY");
        var secretKey = ResolveSensitiveValue(_options.SecretKey, "FILE_STORAGE_SECRET_KEY");

        _minioClient = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(_options.UseSsl)
            .Build();

        // 不再在构造函数中 sync-over-async 调用 EnsureBucketExistsAsync().GetAwaiter().GetResult()。
        // Bucket 确保延迟到首次使用时异步执行（见 EnsureBucketExistsOnceAsync），
        // 避免高并发启动或线程池 starvation 时的死锁风险。
    }

    /// <inheritdoc />
    public async Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string contentType, string category, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("文件名不可为空", nameof(fileName));
        }

        // 延迟确保 Bucket 存在（首次调用时执行，后续跳过）
        await EnsureBucketExistsOnceAsync(ct).ConfigureAwait(false);

        var safeCategory = SanitizeCategory(category);
        var ext = Path.GetExtension(fileName);
        var storedName = $"{safeCategory}/{Guid.NewGuid():N}{ext}";

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(storedName)
            .WithStreamData(stream)
            .WithObjectSize(stream.CanSeek ? stream.Length : -1)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putObjectArgs, ct).ConfigureAwait(false);

        var size = stream.CanSeek ? stream.Length : 0;
        var url = await GetUrlAsync(storedName, ct).ConfigureAwait(false);
        _logger.LogInformation("MinIO 文件上传成功 Url={Url} Size={Size}", url, size);

        return new FileUploadResult(url, size, contentType);
    }

    /// <inheritdoc />
    public async Task<Stream> DownloadAsync(string fileUrl, CancellationToken ct = default)
    {
        // 延迟确保 Bucket 存在（首次调用时执行，后续跳过）
        await EnsureBucketExistsOnceAsync(ct).ConfigureAwait(false);

        var objectName = ResolveObjectName(fileUrl);
        var memoryStream = new MemoryStream();

        var getObjectArgs = new GetObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectName)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream));

        await _minioClient.GetObjectAsync(getObjectArgs, ct).ConfigureAwait(false);
        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string fileUrl, CancellationToken ct = default)
    {
        if (!ValidateUrlInternal(fileUrl))
        {
            return;
        }

        // 延迟确保 Bucket 存在（首次调用时执行，后续跳过）
        await EnsureBucketExistsOnceAsync(ct).ConfigureAwait(false);

        var objectName = ResolveObjectName(fileUrl);

        var removeArgs = new RemoveObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(objectName);

        await _minioClient.RemoveObjectAsync(removeArgs, ct).ConfigureAwait(false);
        _logger.LogInformation("MinIO 文件删除成功 ObjectName={ObjectName}", objectName);
    }

    /// <inheritdoc />
    public Task<string> GetUrlAsync(string fileName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var storedName = fileName.TrimStart('/');
        return Task.FromResult(BuildUrl(storedName));
    }

    /// <inheritdoc />
    public Task<bool> ValidateUrlAsync(string fileUrl, CancellationToken ct = default)
    {
        return Task.FromResult(ValidateUrlInternal(fileUrl));
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string fileUrl, CancellationToken ct = default)
    {
        if (!ValidateUrlInternal(fileUrl))
        {
            return false;
        }

        // 延迟确保 Bucket 存在（首次调用时执行，后续跳过）
        await EnsureBucketExistsOnceAsync(ct).ConfigureAwait(false);

        var objectName = ResolveObjectName(fileUrl);

        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName);

            await _minioClient.StatObjectAsync(statArgs, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is Minio.Exceptions.ObjectNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool ValidateUrlInternal(string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return false;
        }

        var publicUrl = !string.IsNullOrEmpty(_options.PublicUrl)
            ? _options.PublicUrl.TrimEnd('/')
            : BuildBaseUrl();

        return fileUrl.StartsWith(publicUrl + "/", StringComparison.Ordinal)
               || fileUrl.Equals(publicUrl, StringComparison.Ordinal);
    }

    private string ResolveObjectName(string fileUrl)
    {
        var publicUrl = !string.IsNullOrEmpty(_options.PublicUrl)
            ? _options.PublicUrl.TrimEnd('/')
            : BuildBaseUrl();

        var prefix = publicUrl.EndsWith('/') ? publicUrl : publicUrl + "/";
        return fileUrl.StartsWith(prefix, StringComparison.Ordinal)
            ? fileUrl[prefix.Length..]
            : string.Empty;
    }

    private string BuildUrl(string storedName)
    {
        var baseUrl = !string.IsNullOrEmpty(_options.PublicUrl)
            ? _options.PublicUrl.TrimEnd('/')
            : BuildBaseUrl();

        return $"{baseUrl}/{storedName.TrimStart('/')}";
    }

    private string BuildBaseUrl()
    {
        var protocol = _options.UseSsl ? "https" : "http";
        return $"{protocol}://{_options.Endpoint}/{_options.BucketName}";
    }

    private static string SanitizeCategory(string? category)
    {
        return !string.IsNullOrWhiteSpace(category) && AllowedCategories.Contains(category)
            ? category.ToLowerInvariant()
            : "misc";
    }

    /// <summary>
    /// 延迟确保 Bucket 存在（首次使用时异步执行，后续调用直接跳过）。
    /// 使用双重检查锁定 + Volatile.Read/Write 保证线程安全与跨线程可见性。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    private async Task EnsureBucketExistsOnceAsync(CancellationToken ct)
    {
        // 快速路径：已确保则直接返回，无锁
        if (Volatile.Read(ref _bucketEnsured) == 1)
        {
            return;
        }

        await _bucketEnsureLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 二次检查：防止多个线程排队后重复执行
            if (_bucketEnsured == 1)
            {
                return;
            }

            await EnsureBucketExistsAsync(ct).ConfigureAwait(false);
            Volatile.Write(ref _bucketEnsured, 1);
        }
        finally
        {
            _bucketEnsureLock.Release();
        }
    }

    /// <summary>
    /// 确保 Bucket 存在，不存在则创建。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        var bucketExistsArgs = new BucketExistsArgs()
            .WithBucket(_options.BucketName);

        var exists = await _minioClient.BucketExistsAsync(bucketExistsArgs, ct).ConfigureAwait(false);
        if (!exists)
        {
            var makeBucketArgs = new MakeBucketArgs()
                .WithBucket(_options.BucketName);

            await _minioClient.MakeBucketAsync(makeBucketArgs, ct).ConfigureAwait(false);
            _logger.LogInformation("MinIO Bucket 已创建 BucketName={BucketName}", _options.BucketName);
        }
    }

    /// <summary>
    /// 解析敏感参数：优先从环境变量读取，其次使用配置值。
    /// </summary>
    private static string ResolveSensitiveValue(string configValue, string envKey)
    {
        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        if (string.IsNullOrWhiteSpace(configValue))
        {
            throw new InvalidOperationException(
                $"对象存储敏感参数未配置，请设置环境变量 {envKey} 或在配置中提供 FileStorage:ObjectStorage 相关字段。");
        }

        return configValue;
    }
}