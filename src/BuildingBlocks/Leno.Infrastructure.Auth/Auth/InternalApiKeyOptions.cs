namespace Leno.Infrastructure.Auth;

/// <summary>
/// 内部服务间鉴权配置项。
/// </summary>
public sealed class InternalApiKeyOptions
{
    public const string SectionName = "InternalAuth";

    /// <summary>内部 API 密钥，各服务间共享。为空时禁用内部鉴权（仅开发环境）。</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>内部端点路由前缀，默认 "internal/"。仅匹配此前缀的请求需校验内部密钥。</summary>
    public string RoutePrefix { get; set; } = "internal/";
}
