namespace Leno.Infrastructure.Abstractions.Sessions;

/// <summary>在线用户统计指标。</summary>
public sealed class OnlineUserStats
{
    public int Total { get; set; }
    public int Logins24h { get; set; }
    public int Anomalies { get; set; }
}
