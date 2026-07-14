namespace Leno.ApiGateway.Options;

/// <summary>
/// 网关响应缓存配置选项，对应 appsettings.json 中 <c>Gateway:Cache</c> 节。
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Gateway:Cache";

    /// <summary>是否启用缓存中间件。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>默认缓存 TTL，当路径不匹配 <see cref="PathTtls"/> 任何前缀时使用。</summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 路径前缀级 TTL 配置。Key 为路径前缀（如 <c>/api/products/</c>），
    /// Value 为该前缀下所有请求的缓存 TTL。
    /// 匹配规则：选择最长匹配前缀。
    /// </summary>
    public Dictionary<string, TimeSpan> PathTtls { get; set; } = new();

    /// <summary>
    /// 根据请求路径获取缓存 TTL。遍历 <see cref="PathTtls"/> 中所有前缀，
    /// 返回匹配到的最长前缀对应的 TTL；无匹配则返回 <see cref="DefaultTtl"/>。
    /// </summary>
    public TimeSpan GetTtlForPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return DefaultTtl;
        }

        TimeSpan best = DefaultTtl;
        int bestLength = -1;

        foreach (var (prefix, ttl) in PathTtls)
        {
            if (prefix.Length > bestLength
                && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                best = ttl;
                bestLength = prefix.Length;
            }
        }

        return best;
    }
}
