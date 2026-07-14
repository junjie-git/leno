namespace Leno.ApiGateway.Options;

/// <summary>
/// CORS 配置选项，对应 appsettings.json 中 <c>Gateway:Cors</c> 节。
/// <para>
/// <see cref="AllowedOrigins"/> 为启动时的默认 Origin 列表（来自配置文件），
/// 运行时由 <c>ConsulCorsOriginProvider</c> 从 Consul KV 热更新覆盖。
/// </para>
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Gateway:Cors";

    /// <summary>是否启用 CORS 中间件。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>默认允许的 Origin 列表（启动时从配置读取，运行时由 Consul KV 覆盖）。</summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>是否允许任意 HTTP 方法。</summary>
    public bool AllowAnyMethod { get; set; } = true;

    /// <summary>是否允许任意请求头。</summary>
    public bool AllowAnyHeader { get; set; } = true;

    /// <summary>是否允许携带凭证（Cookie 等）。</summary>
    public bool AllowCredentials { get; set; } = true;

    /// <summary>预检请求缓存时长。</summary>
    public TimeSpan PreflightMaxAge { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Consul KV 中存储 Origin 列表的 Key。</summary>
    public string ConsulKvKey { get; set; } = "leno/gateway/cors-origins";

    /// <summary>Origin 列表定时刷新间隔。</summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(1);
}
