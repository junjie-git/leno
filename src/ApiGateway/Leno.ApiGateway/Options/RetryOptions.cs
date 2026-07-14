namespace Leno.ApiGateway.Options;

/// <summary>
/// 重试配置根节，对应 appsettings.json 中 <c>Retry</c> 节。
/// </summary>
public sealed class RetryOptions
{
    /// <summary>最大重试次数（不含首次请求）。</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>退避策略：Linear 或 Exponential。</summary>
    public string Backoff { get; set; } = "Exponential";

    /// <summary>最小退避时间（首次重试前等待）。</summary>
    public TimeSpan MinBackoff { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>最大退避时间（指数退避上限）。</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 触发重试的 HTTP 状态码列表。
    /// YARP 2.2.0 通过 Cluster.Retry.RetryableStatusCodes 配置。
    /// </summary>
    public int[] RetryableStatusCodes { get; set; } = new[] { 503 };

    /// <summary>
    /// 仅幂等方法重试（GET/HEAD/PUT/DELETE/OPTIONS/TRACE）。
    /// YARP 默认即如此；此字段作为元数据用于校验。
    /// </summary>
    public bool IdempotentMethodsOnly { get; set; } = true;
}

/// <summary>
/// 重试路由分类常量，用于在 appsettings 中标注哪些 Cluster 启用重试。
/// </summary>
public static class RetryRouteTypes
{
    /// <summary>启用重试的 Cluster 列表（默认全部 11 个业务 Cluster）。</summary>
    public static readonly HashSet<string> RetryEnabledClusters = new()
    {
        "user-auth", "product", "cart", "order", "promotion",
        "payment", "points", "review-aftersales", "seller-shop",
        "notification", "system-admin"
    };

    /// <summary>不启用重试的 Cluster 列表（文件上传等不可重复操作）。</summary>
    public static readonly HashSet<string> RetryDisabledClusters = new();
}
