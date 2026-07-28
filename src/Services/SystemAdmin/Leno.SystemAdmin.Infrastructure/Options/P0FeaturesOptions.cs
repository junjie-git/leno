namespace Leno.SystemAdmin.Infrastructure.Options;

/// <summary>
/// P0 功能配置选项，对应 appsettings.json 中 <c>P0Features</c> 节。
/// </summary>
public sealed class P0FeaturesOptions
{
    public const string SectionName = "P0Features";

    /// <summary>用户会话配置。</summary>
    public UserSessionOptions UserSession { get; set; } = new();

    /// <summary>服务器监控配置。</summary>
    public ServerMonitorOptions ServerMonitor { get; set; } = new();

    /// <summary>地理定位配置。</summary>
    public GeoLocationOptions GeoLocation { get; set; } = new();
}

/// <summary>用户会话存储配置。</summary>
public sealed class UserSessionOptions
{
    /// <summary>会话 TTL（小时），默认 24。</summary>
    public int SessionTtlHours { get; set; } = 24;

    /// <summary>单用户最大会话数，默认 5。</summary>
    public int MaxSessionsPerUser { get; set; } = 5;
}

/// <summary>服务器监控配置。</summary>
public sealed class ServerMonitorOptions
{
    /// <summary>采样间隔（秒），默认 1。</summary>
    public int SampleIntervalSeconds { get; set; } = 1;

    /// <summary>历史数据最大点数，默认 300。</summary>
    public int HistoryMaxPoints { get; set; } = 300;
}

/// <summary>地理定位配置。</summary>
public sealed class GeoLocationOptions
{
    /// <summary>MaxMind GeoLite2 .mmdb 文件路径。</summary>
    public string MaxMindDbPath { get; set; } = "/var/lib/leno/GeoLite2-City.mmdb";

    /// <summary>MaxMind license key（可选，用于自动下载更新）。</summary>
    public string LicenseKey { get; set; } = string.Empty;
}
