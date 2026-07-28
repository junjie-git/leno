namespace Leno.Infrastructure.Abstractions.Sessions;

/// <summary>
/// 用户会话存储抽象：Identity 登录成功时写入，SystemAdmin 查询与强制下线。
/// 实现位于 SystemAdmin.Infrastructure（RedisUserSessionStore）。
/// </summary>
public interface IUserSessionStore
{
    Task RecordAsync(OnlineUserSession session, CancellationToken ct = default);
    Task<List<OnlineUserSession>> QueryAsync(OnlineUserQuery query, CancellationToken ct = default);
    Task<OnlineUserSession?> GetByIdAsync(string sessionId, CancellationToken ct = default);
    Task<OnlineUserStats> GetStatsAsync(CancellationToken ct = default);
    Task RemoveAsync(string sessionId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default);
}
