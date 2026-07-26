namespace Leno.SystemAdmin.Infrastructure.Options;

/// <summary>
/// Outbox 监控配置。
/// 通过 <c>appsettings.json</c> 的 <c>SystemAdmin:OutboxMonitor</c> 节绑定，
/// 控制各域 Outbox 表的连接信息、积压阈值与趋势采样间隔。
/// </summary>
public sealed class OutboxMonitorOptions
{
    /// <summary>配置节名称。</summary>
    public const string SectionName = "SystemAdmin:OutboxMonitor";

    /// <summary>
    /// 待监控的限界上下文列表。
    /// 每项包含上下文名与连接字符串（指向该域数据库，outbox_messages 表）。
    /// 若某上下文连接字符串为空，则跳过该域查询（功能降级）。
    /// </summary>
    public List<OutboxContextConfig> Contexts { get; set; } = new();

    /// <summary>积压警告阈值：PendingCount 超过此值标记为 Backlog。默认 100。</summary>
    public int BacklogWarningThreshold { get; set; } = 100;

    /// <summary>积压严重阈值：PendingCount 超过此值标记为 SevereBacklog。默认 1000。</summary>
    public int BacklogSevereThreshold { get; set; } = 1000;

    /// <summary>趋势采样间隔（分钟），默认 30。</summary>
    public int TrendSampleIntervalMinutes { get; set; } = 30;
}

/// <summary>
/// 单个限界上下文的 Outbox 监控配置。
/// </summary>
public sealed class OutboxContextConfig
{
    /// <summary>限界上下文名，如 Order、Payment。</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// 该域数据库的连接字符串，指向 outbox_messages 表所在的库。
    /// 为空时跳过该域查询。
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
