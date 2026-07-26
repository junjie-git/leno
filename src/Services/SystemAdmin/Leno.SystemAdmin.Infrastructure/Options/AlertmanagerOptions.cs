namespace Leno.SystemAdmin.Infrastructure.Options;

/// <summary>
/// Alertmanager 集成配置。
/// 通过 <c>appsettings.json</c> 的 <c>SystemAdmin:Alertmanager</c> 节绑定，
/// 控制 Alertmanager HTTP API 端点、超时与认证令牌。
/// </summary>
public sealed class AlertmanagerOptions
{
    /// <summary>配置节名称。</summary>
    public const string SectionName = "SystemAdmin:Alertmanager";

    /// <summary>
    /// Alertmanager 基础地址，如 http://alertmanager:9093。
    /// 为空时客户端返回空结果（功能降级），便于无 Alertmanager 环境下启动。
    /// </summary>
    public string BaseAddress { get; set; } = string.Empty;

    /// <summary>HTTP 请求超时（秒），默认 10。</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>认证令牌（Bearer），可空。 Alertmanager 未启用认证时留空。</summary>
    public string? AuthToken { get; set; }

    /// <summary>是否启用；为 false 时客户端返回空结果。默认 true。</summary>
    public bool Enabled { get; set; } = true;
}
