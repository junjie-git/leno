namespace Leno.Infrastructure.Abstractions.Sessions;

/// <summary>
/// 在线用户会话投影：存储在 Redis，不进入 EF Core DbContext。
/// 由 Identity 登录流程通过 IUserSessionStore.RecordAsync 写入，
/// SystemAdmin 通过 IUserSessionStore 查询与强制下线。
/// </summary>
public sealed class OnlineUserSession
{
    public string SessionId { get; set; } = string.Empty;       // JWT jti
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public string IpAddress { get; set; } = string.Empty;
    public string? GeoLocation { get; set; }
    public string Browser { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string TokenPreview { get; set; } = string.Empty;    // 前 8 位
    public string? DeviceFingerprint { get; set; }
    public int RequestCount { get; set; }
    public DateTime LoginAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public bool IsAnomaly { get; set; }
}
