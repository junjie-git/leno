namespace Leno.Infrastructure.Storage;

/// <summary>
/// 文件存储总配置，按 Provider 切换本地磁盘或对象存储（MinIO/OSS）适配器。
/// 对应 appsettings.json 中 <c>FileStorage</c> 节。
/// </summary>
public sealed class FileStorageOptions
{
    /// <summary>存储提供商标识，取值 Local / MinIO / OSS。</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>本地磁盘存储配置（Provider=Local 时使用）。</summary>
    public LocalStorageOptions? Local { get; set; }

    /// <summary>对象存储配置（Provider=MinIO/OSS 时使用，预留适配器后续按需实现）。</summary>
    public ObjectStorageOptions? ObjectStorage { get; set; }
}

/// <summary>
/// 本地磁盘存储配置。
/// </summary>
public sealed class LocalStorageOptions
{
    public string BasePath { get; set; } = default!;

    public string BaseUrl { get; set; } = default!;

    /// <summary>单文件大小上限（字节），默认 10MB。</summary>
    public long MaxFileSize { get; set; } = 10485760;

    /// <summary>允许的文件扩展名（小写、不含点）。为空表示不限制。</summary>
    public HashSet<string> AllowedExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 对象存储配置（MinIO/OSS），预留以支持后续对象存储适配器。
/// </summary>
public sealed class ObjectStorageOptions
{
    public string Provider { get; set; } = default!;

    public string Endpoint { get; set; } = default!;

    public string AccessKey { get; set; } = default!;

    public string SecretKey { get; set; } = default!;

    public string BucketName { get; set; } = default!;

    public string PublicUrl { get; set; } = default!;
}
