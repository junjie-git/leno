namespace Leno.SystemAdmin.Application.DTOs;

/// <summary>
/// 在线用户 DTO，对应前端 spec §3.4。
/// SessionDurationMs 为派生字段，由应用层 DateTime.UtcNow - LoginAt 实时计算。
/// </summary>
public sealed class OnlineUserDto
{
    /// <summary>会话标识（JWT jti）。</summary>
    public string SessionId { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];

    public string IpAddress { get; set; } = string.Empty;

    public string? GeoLocation { get; set; }

    public string Browser { get; set; } = string.Empty;

    public string Os { get; set; } = string.Empty;

    /// <summary>访问令牌前 8 位预览。</summary>
    public string TokenPreview { get; set; } = string.Empty;

    public string? DeviceFingerprint { get; set; }

    public int RequestCount { get; set; }

    public DateTime LoginAt { get; set; }

    public DateTime LastActivityAt { get; set; }

    /// <summary>会话时长（毫秒），由 LoginAt 实时派生。</summary>
    public long SessionDurationMs { get; set; }

    /// <summary>是否为异常会话（多设备或异地登录）。</summary>
    public bool IsAnomaly { get; set; }
}

/// <summary>在线用户统计 DTO。</summary>
public sealed class OnlineUserStatsDto
{
    /// <summary>当前在线总数。</summary>
    public int Total { get; set; }

    /// <summary>近 24 小时登录数。</summary>
    public int Logins24h { get; set; }

    /// <summary>异常会话数。</summary>
    public int Anomalies { get; set; }
}

/// <summary>在线用户分页查询结果。</summary>
public sealed class OnlineUserListResultDto
{
    public List<OnlineUserDto> Items { get; set; } = [];

    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
