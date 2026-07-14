namespace Leno.ApiGateway.Options;

/// <summary>
/// 超时配置根节，对应 appsettings.json 中 <c>Timeout</c> 节。
/// 与 YARP 路由级 <c>Timeout</c>/<c>TimeoutPolicy</c> 字段配套使用。
/// </summary>
public sealed class TimeoutOptions
{
    /// <summary>命名超时策略映射，Key 为策略名（与路由 TimeoutPolicy 字段对应）。</summary>
    public Dictionary<string, TimeoutPolicyOptions> Policies { get; set; } = new();

    /// <summary>默认超时策略名（无显式 TimeoutPolicy 的路由使用）。</summary>
    public string DefaultPolicy { get; set; } = "leno-default";
}

/// <summary>命名超时策略配置。</summary>
public sealed class TimeoutPolicyOptions
{
    /// <summary>路由类型标签：default/seckill/upload/internal。</summary>
    public string RouteType { get; set; } = "default";

    /// <summary>整体请求超时（端到端，包括 YARP 转发与后端处理）。</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 连接超时（HttpClient 连接到后端的超时）。
    /// YARP 通过 Cluster.HttpClient 配置间接控制；此字段仅作为元数据用于校验。
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 读取超时（HttpClient 读取后端响应字节的 idle 超时）。
    /// 对应 YARP Cluster.HttpRequest.ActivityTimeout。
    /// </summary>
    public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>说明（用于运维参考，不影响运行时行为）。</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 预置的路由类型常量，与 appsettings.json 中 Timeout:Policies 的 Key 对应。
/// </summary>
public static class TimeoutRouteTypes
{
    public const string Default = "default";
    public const string Seckill = "seckill";
    public const string Upload = "upload";
    public const string Internal = "internal";
}
